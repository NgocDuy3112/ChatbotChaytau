# AI Chat with Custom Data

This project is an AI chat application that demonstrates how to chat with custom data using an AI language model. Please note that this template is currently in an early preview stage. If you have feedback, please take a [brief survey](https://aka.ms/dotnet-chat-templatePreview2-survey).

>[!NOTE]
> Before running this project you need to configure the API keys or endpoints for the providers you have chosen. See below for details specific to your choices.

# Configure the AI Model Provider
## Using OpenAI

To call the OpenAI REST API, you will need an API key. To obtain one, first [create a new OpenAI account](https://platform.openai.com/signup) or [log in](https://platform.openai.com/login). Next, navigate to the API key page and select "Create new secret key", optionally naming the key. Make sure to save your API key somewhere safe and do not share it with anyone.

From the command line, configure your API key for this project using .NET User Secrets by running the following commands:

```sh
cd <<your-project-directory>>
dotnet user-secrets set OpenAI:Key YOUR-API-KEY
```


---

## Production deployment checklist

### Required environment (`.env` — see `.env.example`)

| Key | Purpose |
|---|---|
| `OpenRouter__ApiKey` | LLM calls |
| `Authentication__Google__*` | login (app **fails fast at boot** in Production if missing) |
| `ConnectionStrings__Postgres`, `POSTGRES_*` | database |
| `WebSearch__ApiKey` | optional Tavily key — enables the agent's `web_search` tool |
| `ASPNETCORE_ENVIRONMENT` | `Production` behind the proxy |
| `Admin__Emails__0` = your Google email | **grants access to `/admin`** (or add to `appsettings.json` `Admin:Emails` — that file is committed, so only add non-secret values you're happy to share) |

### Stack

```sh
docker compose up -d        # app + postgres + redis (all healthchecked)
```

- **Redis** (`Redis:Enabled=true` only in compose): DataProtection keys
  (logins survive redeploys) + shared chat rate limiting + conversation-list cache.
  Local `dotnet run` falls back to memory automatically (`Redis:Enabled=false`).
- **Database migrations**: compose applies them on boot (`Database__AutoMigrate=true`).
  For a local DB run `dotnet ef database update` once (pending: `AddUsageEvents`).
- **Rate limit**: `RateLimit:ChatPerMinute` (default 20, `0` disables).
- **Model pricing** (`OpenRouter:Pricing` in `appsettings.json`): USD per 1M
  tokens, seeded from the live OpenRouter catalog. Unlisted models show as
  "unpriced" in `/admin` — update prices when you add models.
- **Forwarded headers** are trusted from any address (single-proxy setup) so the
  Google OAuth callback sees `https`. If you expose the app *directly* to the
  internet without a proxy, revisit `ForwardedHeadersOptions` in `Program.cs`.
- **Admin portal**: `/admin` requires the `Admin` role — members of
  `Admin:Emails` receive it automatically at Google login (dev user gets it via
  `/dev-login` in Development).

### Known local-dev gotcha
`ConnectionStrings__Postgres` in `.env` must match your running Postgres —
if `dotnet ef` reports `28P01 password authentication failed`, the password
in `.env` is stale for that server.
