# SaoKeBot

A personal finance Telegram bot with a web dashboard. Send the bot a short message like
`Coffee 30k` and it records the transaction; open `/dashboard` to see charts and monthly
statistics. Each user's data is fully isolated by their own Telegram ID.

Built with ASP.NET Core (.NET 8), Razor Pages, and Telegram.Bot. Storage is pluggable:
SQLite for local use, DynamoDB for AWS.

## Live demo

Try a running instance on Telegram: **[@SaoKeDaiVuong_Bot](https://t.me/SaoKeDaiVuong_Bot)**

Open the chat, send `/request` to ask for access, then try a transaction like `Coffee 30k`
and `/dashboard`.

## Features

- **Natural-language input** — `Lunch 45k`, `Salary 10m`, etc. Amounts support `k`
  (thousand), `tr` / `m` (million).
- **Transaction types** — income, expense, lending (others owe you), and debt (you owe
  others).
- **Web dashboard** — per-user, token-protected pages with a spending pie chart, monthly
  totals, and transaction history.
- **Whitelist + admin approval** — new users request access with `/request`; admins
  approve/reject inline.
- **Per-user isolation** — every user has their own independent ledger keyed by Telegram
  ID. No data is shared between accounts.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- A Telegram bot token from [@BotFather](https://t.me/BotFather)

## Setup

1. **Clone and restore**

   ```bash
   dotnet restore
   ```

2. **Configure** — edit `appsettings.json` (or use environment variables, see below):

   ```json
   "BotConfiguration": {
     "BotToken": "<your Telegram bot token>",
     "ConnectionString": "Data Source=data/saoke.db",
     "DashboardSecret": "<long random string>",
     "WebUrl": "http://localhost:5000"
   }
   ```

   - `BotToken` — from @BotFather.
   - `DashboardSecret` — any long random string used to sign dashboard links
     (e.g. `openssl rand -base64 48`). Keep it stable.
   - `WebUrl` — the base URL where the dashboard is reachable.

   Instead of editing the file you can set environment variables (double underscore maps
   to the nested JSON key):

   ```bash
   BotConfiguration__BotToken=...
   BotConfiguration__DashboardSecret=...
   BotConfiguration__WebUrl=http://localhost:5000
   ```

3. **Set admins and whitelist** — `Config/admins.json` holds Telegram IDs that can approve
   users; `Config/whitelist.json` holds IDs allowed to use the bot. Add your own ID to both
   to get started:

   ```json
   [ 123456789 ]
   ```

4. **Run**

   ```bash
   dotnet run
   ```

   The web dashboard listens on `http://localhost:5000` by default and the bot starts
   polling immediately.

## How to use the bot

| You send            | Result                                             |
| ------------------- | -------------------------------------------------- |
| `Coffee 30k`        | Expense of 30,000 in the matched category          |
| `Salary 12m`        | Income of 12,000,000                               |
| `/dashboard`        | A private link to your web dashboard               |
| `undo`              | Deletes your most recent transaction               |
| `/request`          | (Non-whitelisted users) asks admins for access     |
| `/help`             | Shows the syntax and available commands            |

### Loans & debts (tracked per person)

These four commands follow the form `command <name> <amount>` and keep a running balance
per counterparty. Settling a loan/debt reduces that person's outstanding balance **without
changing your total balance** (a receivable/liability simply turns back into cash).

| You send             | Result                                                    |
| -------------------- | --------------------------------------------------------- |
| `lend Long 500k`     | You lent 500,000 to Long (Long now owes you 500,000)      |
| `getback Long 200k`  | Long repaid you 200,000 (Long now owes you 300,000)       |
| `borrow Hoa 1m`      | You borrowed 1,000,000 from Hoa (you owe Hoa 1,000,000)   |
| `payback Hoa 400k`   | You repaid Hoa 400,000 (you now owe Hoa 600,000)          |

Both **partial** and **full** settlements are supported — send the exact remaining amount
to bring a balance to zero. The name may contain spaces (e.g., `lend Anh Long 500k`). The
dashboard shows a per-person breakdown of who owes you and whom you owe.

## Categories & keywords

Transactions are categorized by matching keywords against your message. The keyword lists
in `Config/keywords.json` ship **empty** so you can fill them with words that fit your own
language and habits. The category **name** determines the transaction type:

- `Income` → counted as income (IN)
- `Lending` → money others owe you (LOAN)
- `I Owe` → money you owe others (DEBT)
- any other category → an expense (OUT)

Add keywords like this:

```json
{
  "Income":  ["salary", "bonus"],
  "Lending": ["lent", "loaned"],
  "I Owe":   ["borrowed", "owe"],
  "Food":    ["coffee", "lunch", "dinner", "groceries"],
  "Transport": ["gas", "taxi", "bus"],
  "Other": []
}
```

Longer, more specific keywords are matched first. Any message that matches no keyword falls
back to the `Other` expense category. Edits to `keywords.json` are picked up on restart.

## Storage

- **SQLite (default)** — set `Storage:Provider` to `Sqlite`. Data is stored in
  `data/saoke.db`.
- **DynamoDB** — set `Storage:Provider` to `DynamoDB` and `Storage:TableName`. The table is
  created automatically on first run (requires appropriate AWS credentials and IAM
  permissions).

## Project structure

```
Program.cs                 App startup & dependency injection
Models/                    Data models (Transaction, config, parse result)
Services/                  Bot service, message parser, auth, config, tokens
Services/Features/         Per-type transaction handlers (income/expense/debt/loan)
Services/Repositories/     Storage layer (SQLite + DynamoDB)
Pages/                     Razor Pages dashboard (Index + History)
Config/                    admins.json, whitelist.json, keywords.json
```

## Security notes

- Dashboard links are protected by an HMAC-SHA256 token bound to each user ID; a user can
  only ever view their own data.
- Never commit real secrets. `.env` and `data/*.db` are gitignored.
