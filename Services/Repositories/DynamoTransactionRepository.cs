using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using SaoKeBot.Models;

namespace SaoKeBot.Services.Repositories
{
    // Amazon DynamoDB-backed storage for production on AWS.
    // Key model: PK = uid (number), SK = "yyyy-MM-dd HH:mm:ss.fffffff#guid" (time-ordered).
    // The SK starts with the date, so: filter by month = begins_with(sk, "yyyy-MM"); undo = largest SK.
    public class DynamoTransactionRepository : ITransactionRepository
    {
        private readonly IAmazonDynamoDB _db;
        private readonly string _table;
        private const string DateFmt = "yyyy-MM-dd HH:mm:ss";

        public DynamoTransactionRepository(IAmazonDynamoDB db, string tableName = "SaoKeTransactions")
        {
            _db = db;
            _table = tableName;
        }

        // Create the table if missing and wait until ACTIVE. Requires CreateTable/DescribeTable IAM.
        public async Task InitAsync()
        {
            try
            {
                await _db.DescribeTableAsync(_table);
                return; // already exists.
            }
            catch (ResourceNotFoundException) { /* not found -> create. */ }

            await _db.CreateTableAsync(new CreateTableRequest
            {
                TableName = _table,
                BillingMode = BillingMode.PAY_PER_REQUEST, // on-demand (cheap for a small bot).
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new("uid", ScalarAttributeType.N),
                    new("sk", ScalarAttributeType.S)
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new("uid", KeyType.HASH),
                    new("sk", KeyType.RANGE)
                }
            });

            // Wait until the table becomes ACTIVE.
            for (int i = 0; i < 30; i++)
            {
                var d = await _db.DescribeTableAsync(_table);
                if (d.Table.TableStatus == TableStatus.ACTIVE) break;
                await Task.Delay(1000);
            }
        }

        public async Task AddAsync(long userId, ParseResult result)
        {
            var now = DateTime.Now;
            var sk = $"{now:yyyy-MM-dd HH:mm:ss.fffffff}#{Guid.NewGuid():N}";
            await _db.PutItemAsync(new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["uid"] = new() { N = userId.ToString() },
                    ["sk"] = new() { S = sk },
                    ["amount"] = new() { N = result.Amount.ToString(CultureInfo.InvariantCulture) },
                    ["note"] = new() { S = result.Note ?? "" },
                    ["category"] = new() { S = result.Category ?? "" },
                    ["type"] = new() { S = result.Type ?? "" },
                    ["date"] = new() { S = now.ToString(DateFmt, CultureInfo.InvariantCulture) }
                }
            });
        }

        public async Task<Transaction?> UndoLastAsync(long userId)
        {
            var resp = await _db.QueryAsync(new QueryRequest
            {
                TableName = _table,
                KeyConditionExpression = "uid = :u",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":u"] = new() { N = userId.ToString() } },
                ScanIndexForward = false, // newest first.
                Limit = 1
            });

            if (resp.Items.Count == 0) return null;
            var item = resp.Items[0];

            await _db.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["uid"] = new() { N = userId.ToString() },
                    ["sk"] = item["sk"]
                }
            });

            return Map(item);
        }

        public async Task<List<Transaction>> GetAllAsync(long userId)
        {
            var items = await QueryAllAsync("uid = :u",
                new Dictionary<string, AttributeValue> { [":u"] = new() { N = userId.ToString() } });
            return items.Select(Map).ToList();
        }

        public async Task<List<Transaction>> GetByMonthAsync(long userId, string yyyyMM)
        {
            var items = await QueryAllAsync("uid = :u AND begins_with(sk, :m)",
                new Dictionary<string, AttributeValue>
                {
                    [":u"] = new() { N = userId.ToString() },
                    [":m"] = new() { S = yyyyMM }
                });
            // Newest first for the UI.
            return items.Select(Map).OrderByDescending(t => t.Date).ToList();
        }

        public async Task<List<string>> GetAvailableMonthsAsync(long userId)
        {
            var all = await GetAllAsync(userId);
            return all.Select(t => t.Date.ToString("yyyy-MM"))
                      .Distinct()
                      .OrderByDescending(m => m)
                      .ToList();
        }

        // Paginated query (DynamoDB returns at most 1MB per page).
        private async Task<List<Dictionary<string, AttributeValue>>> QueryAllAsync(
            string keyExpr, Dictionary<string, AttributeValue> values)
        {
            var results = new List<Dictionary<string, AttributeValue>>();
            Dictionary<string, AttributeValue>? lastKey = null;
            do
            {
                var resp = await _db.QueryAsync(new QueryRequest
                {
                    TableName = _table,
                    KeyConditionExpression = keyExpr,
                    ExpressionAttributeValues = values,
                    ExclusiveStartKey = lastKey
                });
                results.AddRange(resp.Items);
                lastKey = resp.LastEvaluatedKey != null && resp.LastEvaluatedKey.Count > 0
                    ? resp.LastEvaluatedKey : null;
            } while (lastKey != null);
            return results;
        }

        private static Transaction Map(Dictionary<string, AttributeValue> i)
        {
            return new Transaction
            {
                user_id = long.TryParse(GetN(i, "uid"), out var u) ? u : 0,
                Amount = double.TryParse(GetN(i, "amount"), NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ? a : 0,
                Note = GetS(i, "note"),
                Category = GetS(i, "category"),
                Type = GetS(i, "type"),
                Date = DateTime.TryParse(GetS(i, "date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue
            };
        }

        private static string GetS(Dictionary<string, AttributeValue> i, string k) => i.TryGetValue(k, out var v) ? v.S ?? "" : "";
        private static string GetN(Dictionary<string, AttributeValue> i, string k) => i.TryGetValue(k, out var v) ? v.N ?? "0" : "0";
    }
}
