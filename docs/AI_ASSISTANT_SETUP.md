# AI Assistant Setup Guide

This document explains how to set up and configure the **AI Assistant** feature in the Personal Finance App. The AI assistant is a floating chat widget that provides personalized financial insights using OpenAI's GPT-4o-mini model.

---

## 1. What the AI Assistant Does

The AI assistant is accessible via a floating chat bubble in the bottom-right corner of the application. When opened, it provides:

- **Personalized financial Q&A** — answers questions based on your net worth parameters, budget allocations, and savings goals.
- **Budget analysis** — reviews your budget allocations and suggests improvements.
- **Net worth insights** — analyzes your net worth progress and retirement projections.
- **Savings tips** — provides recommendations based on your savings goals.
- **General financial advice** — offers educational financial guidance.

### How It Works

1. You type a message in the chat widget.
2. The frontend sends your message to the backend API.
3. The backend gathers your financial data (net worth parameters, budget allocations, savings goals).
4. A personalized system prompt is built with your financial context.
5. The request is sent to OpenAI's GPT-4o-mini model.
6. The AI's response is returned and displayed in the chat.

---

## 2. Prerequisites

### OpenAI Account

You need an **OpenAI account** with API access:

1. Go to [https://platform.openai.com/](https://platform.openai.com/) and sign up or log in.
2. Navigate to **API Keys** in the dashboard: [https://platform.openai.com/api-keys](https://platform.openai.com/api-keys).
3. Click **"Create new secret key"** and copy the generated key.
4. **Store the key securely** — you will need it for configuration.

> **Important:** Never commit your API key to Git or share it publicly. The key provides access to your OpenAI account and billing.

### OpenAI Billing

- You must have a **payment method** added to your OpenAI account.
- OpenAI requires a minimum credit balance (typically $5) for API access.
- The AI assistant uses **GPT-4o-mini**, which is one of OpenAI's most cost-effective models.

---

## 3. Configuration

### Development Environment (Local)

For local development, use **.NET User Secrets** to store the API key securely:

```bash
# Set the OpenAI API key in the ApiService project
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-api-key-here" --project PersonalFinance/src/PersonalFinance.ApiService

# Verify the secret is set
dotnet user-secrets list --project PersonalFinance/src/PersonalFinance.ApiService
```

The expected output should show:

```
OpenAI:ApiKey = sk-proj-your-api-key-here
```

### Optional: Custom Model Configuration

By default, the AI assistant uses `gpt-4o-mini`. The defaults live in `appsettings.json` of the `PersonalFinance.ApiService` project; override any of them in `appsettings.Development.json`:

```json
{
  "OpenAI": {
    "Model": "gpt-4o-mini",
    "MaxTokens": 400,
    "DailyMessageLimit": 30,
    "MonthlyBudgetUsd": 10,
    "BudgetWarningThreshold": 0.9
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `OpenAI:Model` | `gpt-4o-mini` | The OpenAI model to use. Other options: `gpt-4o`, `gpt-3.5-turbo`. The cost estimate always uses gpt-4o-mini prices (see section 4). |
| `OpenAI:MaxTokens` | `400` | Maximum output tokens per model call, for chat and news summaries. Purpose Notes use a fixed 220. |
| `OpenAI:DailyMessageLimit` | `30` | Requests per Account per day (chat, summaries and Purpose Notes together). Accounts with the Admin Role skip this limit. |
| `OpenAI:MonthlyBudgetUsd` | `10` | Estimated spend, in USD, across all Accounts per calendar month. Above it every request is refused until the next month. |
| `OpenAI:BudgetWarningThreshold` | `0.9` | Fraction of the monthly budget at which replies carry a budget warning. |

The temperature is not configurable: requests send none, so OpenAI's own default applies.

### Production Environment (Azure)

For production deployment on Azure, configure the API key using one of the following methods:

#### Option 1: GitHub Actions Pipeline (Recommended)

The project's GitHub Actions deployment pipeline automatically passes the OpenAI API key from GitHub Secrets to Azure during deployment. To configure it:

1. Go to your GitHub repository **Settings** → **Secrets and variables** → **Actions**.
2. Click **New repository secret**.
3. Enter the name: **`OPENAI_API_KEY`**.
4. Paste your OpenAI API key as the value.
5. Click **Add secret**.

The pipeline will automatically pass the key to the Bicep deployment as a secure parameter. The key is stored as a **Container Apps managed secret** and exposed to the API service container as the `OpenAI__ApiKey` environment variable. The double underscore (`__`) is the .NET configuration convention for nested keys, mapping to `OpenAI:ApiKey` in the application.

#### Option 2: Bicep Deployment (Manual)

If deploying manually (not via GitHub Actions), pass your API key as a parameter:

```bash
# Using azd (Azure Developer CLI)
azd deploy --param openAiApiKey="sk-proj-your-api-key-here"

# Using Azure CLI directly
az deployment group create `
  --resource-group <your-resource-group> `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.json `
  --parameters openAiApiKey="sk-proj-your-api-key-here"
```

The key is stored as a **Container Apps managed secret** and exposed to the API service container as the `OpenAI__ApiKey` environment variable.

#### Option 3: Azure Portal Manual Configuration

If you've already deployed and want to add the key without redeploying:

1. Go to the [Azure Portal](https://portal.azure.com).
2. Navigate to your **Container Apps** resource group.
3. Open the **API Service** Container App (named `api-pf-prod-...`).
4. Go to **Settings** > **Environment variables**.
5. Click **Add** and create a new secret-linked environment variable:
   - **Name:** `OpenAI__ApiKey`
   - **Value:** Your OpenAI API key
   - Alternatively, use the **Secrets** tab to add the secret first, then reference it.
6. Click **Apply** and **Restart** the Container App.

#### Option 4: Azure Key Vault (Enterprise)

1. Store the API key in Azure Key Vault as a secret (e.g., `openai-api-key`).
2. Reference the secret in your Azure Container Apps environment variables using Key Vault references.
3. The application will automatically pick up the value through the configuration system.

---

## 4. Cost Estimation

The AI assistant uses **GPT-4o-mini** with the following pricing. `AiQuotaService` holds the same figures and uses them to estimate the cost of every request it records:

| Operation | Price |
|---|---|
| Input tokens | $0.15 per 1M tokens |
| Cached input tokens | $0.075 per 1M tokens |
| Output tokens | $0.60 per 1M tokens |

### Typical Usage Costs

Each chat request includes:
- **System prompt** (~450 tokens): the AI's role, rules and tool list. It is the same for every user up to the reply Language line, so OpenAI can serve it as cached input.
- **Financial snapshot** (~100–300 tokens): income, spending, net worth, budget allocations and savings goal totals.
- **Conversation history**: the last 10 messages, each cut to 2,000 characters.
- **User message**: your question or request.
- **Tool results**: the model fetches budget, transaction (at most 50 rows), savings goal or net worth details on demand, in up to 3 tool rounds. Each round is another model call that resends the conversation so far.
- **AI response**: at most 400 output tokens per model call (`OpenAI:MaxTokens`).

**Estimated cost per exchange:** about $0.001 for a short question with one tool call (two model calls, ~4,000 input and ~400 output tokens). The worst case, with full history and three tool rounds, is about $0.006.

**Monthly estimate:** an Account that uses all 30 daily messages every day sends about 900 a month, which costs roughly $1–$5. All Accounts together stop at the $10 monthly budget.

### Cost Control Measures

The implementation includes the following cost controls:
- **Daily limit per Account:** 30 requests a day (`OpenAI:DailyMessageLimit`). Admins skip it, but not the monthly budget.
- **Monthly budget for all Accounts:** $10 of estimated spend (`OpenAI:MonthlyBudgetUsd`), with a warning on replies from 90%.
- **Output cap:** 400 tokens per model call (`OpenAI:MaxTokens`).
- **Conversation history is limited** to the last 10 messages, each cut to 2,000 characters.
- **Data on demand:** detailed data is fetched through at most 3 tool rounds, not sent with every request.
- **Cache-friendly prompt:** the stable system prompt comes first, so repeated requests pay the cached input price.
- **Off-topic questions are refused** by the scope filter before any model call.
- **GPT-4o-mini** is used by default (cheapest GPT-4 class model).

---

## 5. Verifying the Setup

### Test the AI Assistant Locally

1. **Set the API key** using User Secrets (Step 3 above).
2. **Build and run** the application:
   ```bash
   dotnet run --project PersonalFinance/src/PersonalFinance.AppHost
   ```
3. **Sign in** to the application.
4. **Click the chat bubble** in the bottom-right corner.
5. **Type a message**, e.g., "What is my current budget allocation?"
6. **Verify** that the AI responds with a personalized answer.

### If the AI Assistant Shows an Error Message

| Error Message | Cause | Solution |
|---|---|---|
| "AI assistant is currently unavailable. Please configure the OpenAI API key." | API key is not set or empty. | Set the `OpenAI:ApiKey` user secret. |
| "I'm sorry, I encountered an error processing your request." | API key is invalid, expired, or has insufficient credits. | Verify the API key in your OpenAI dashboard. |
| Network timeout or 502 Bad Gateway | OpenAI API is unreachable or rate-limited. | Check your internet connection and OpenAI API status. |

### Check API Key Status

```bash
# List configured secrets
dotnet user-secrets list --project PersonalFinance/src/PersonalFinance.ApiService

# Clear and reset if needed
dotnet user-secrets clear --project PersonalFinance/src/PersonalFinance.ApiService
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-new-api-key" --project PersonalFinance/src/PersonalFinance.ApiService
```

---

## 6. Architecture Overview

### Components

| Component | Location | Responsibility |
|---|---|---|
| **AI Chat Widget (JS)** | `PersonalFinance.Web/wwwroot/js/core/ai-chat-widget.js` | Floating chat bubble, message display, user input. |
| **AI Chat Widget (CSS)** | `PersonalFinance.Web/wwwroot/css/ai-chat-widget.css` | Styling for the chat panel and bubble. |
| **Web Controller** | `PersonalFinance.Web/Controllers/AiAssistantController.cs` | Receives chat requests from the frontend, forwards to the API service. |
| **Refit API Contract** | `PersonalFinance.Shared/Contracts/IAiAssistantApi.cs` | Type-safe HTTP client interface for AI assistant calls. |
| **API Controller** | `PersonalFinance.ApiService/Features/AiAssistant/AiAssistantController.cs` | Backend endpoint that processes chat requests. |
| **AI Assistant Service** | `PersonalFinance.ApiService/Features/AiAssistant/AiAssistantService.cs` | Gathers user financial data, builds prompts, calls OpenAI API. |
| **DTOs** | `PersonalFinance.Shared/DTOs/AiChatDto.cs` | Request and response data transfer objects. |

### Data Flow

```
User types message
    ↓
AI Chat Widget (JavaScript)
    ↓ POST /api/AiAssistant/chat
Web Controller (PersonalFinance.Web)
    ↓ Refit HTTP call
API Controller (PersonalFinance.ApiService)
    ↓
AI Assistant Service
    ↓ Queries database for user's financial context
    ↓ Builds system prompt with financial data
    ↓ HTTP request to OpenAI API (gpt-4o-mini)
    ↓ Receives AI response
Response flows back through the same path to the user
```

---

## 7. Security Considerations

### API Key Protection

- **Development:** API key is stored in .NET User Secrets (`secrets.json`), which is git-ignored by default.
- **Production:** API key should be stored in Azure Key Vault or as a secure environment variable.
- **Never** hardcode the API key in source code or commit it to version control.

### User Data Privacy

- The AI assistant sends your financial data (net worth, budget, savings goals) to OpenAI's API as part of the prompt.
- OpenAI's API terms state that customer data is **not used to train models** for paid API tiers.
- Only the authenticated user's own data is sent — no other users' data is included.

### Input and Output Safety

- User messages are validated on the server side.
- The AI response is rendered using `textContent` (not `innerHTML`) to prevent XSS attacks.
- The system prompt includes instructions for the AI to provide disclaimers about financial advice.

---

## 8. Troubleshooting

### Common Issues

#### 1. "AI assistant is currently unavailable"

**Cause:** The `OpenAI:ApiKey` configuration value is empty or not set.

**Solution:**
```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-api-key-here" --project PersonalFinance/src/PersonalFinance.ApiService
```
Then restart the application.

#### 2. "I encountered an error processing your request"

**Possible causes:**
- Invalid or expired API key.
- Insufficient credits in your OpenAI account.
- Network connectivity issues.
- OpenAI API rate limiting.

**Solution:**
1. Check your OpenAI dashboard for API key status and account balance.
2. Verify your internet connection.
3. Check the application logs for detailed error messages:
   ```bash
   # Check logs in the ApiService project output
   ```

#### 3. Chat widget is not visible

**Possible causes:**
- You are not signed in — the widget only appears for authenticated users.
- The JavaScript file is not loaded.

**Solution:**
1. Sign in to the application.
2. Check the browser console (F12) for JavaScript errors.
3. Verify that `ai-chat-widget.js` is referenced in `_Layout.cshtml`.

#### 4. AI responses are generic, not personalized

**Possible causes:**
- You have not entered any financial data (net worth parameters, budget, savings goals).
- The database query for your user ID is returning no results.

**Solution:**
1. Complete your net worth parameters in the Net Worth section.
2. Set up budget allocations in the Budget section.
3. Add savings goals in the Savings Goals section.

### Checking Application Logs

The AI assistant logs the following events:

| Log Level | Message | When |
|---|---|---|
| `Warning` | "OpenAI API key is not configured" | When the API key is missing. |
| `Information` | "User {UserId} sent a message to AI assistant" | On successful API call. |
| `Error` | "Error sending message to OpenAI" | When the OpenAI API call fails. |

Check the console output or log files for these messages to diagnose issues.

---

## 9. Disabling the AI Assistant

If you want to temporarily disable the AI assistant without removing the code:

### Option 1: Remove the API Key

```bash
dotnet user-secrets remove "OpenAI:ApiKey" --project PersonalFinance/src/PersonalFinance.ApiService
```

The chat widget will still appear, but the AI will respond with an "unavailable" message.

### Option 2: Hide the Widget

The chat widget is only shown to authenticated users. To hide it completely, remove or comment out the script reference in `PersonalFinance.Web/Views/Shared/_Layout.cshtml`:

```html
<!-- Comment out or remove this line to hide the widget -->
<!-- <script src="~/js/core/ai-chat-widget.js" asp-append-version="true"></script> -->
```

---

## 10. Frequently Asked Questions

### Q: Does the AI assistant store chat history in the database?

**A:** No. Chat history is maintained only in the browser session (JavaScript memory). When you refresh the page or close the browser, the conversation history is cleared. No chat data is persisted to the database.

### Q: Can I use a different AI model?

**A:** Yes. Change the `OpenAI:Model` configuration value to any supported OpenAI model, such as `gpt-4o` or `gpt-3.5-turbo`. Note that more advanced models will increase costs.

### Q: Is the AI assistant available to anonymous (not signed in) users?

**A:** No. The AI assistant widget only appears for authenticated users, and the API endpoint requires authentication.

### Q: How much data is sent to OpenAI?

**A:** Each request sends:
- A system prompt (~300–500 tokens) describing the AI's role.
- Your financial context (net worth parameters, budget allocations, savings goals).
- Up to 10 previous conversation messages.
- Your current message.

### Q: Can I use Azure OpenAI instead of OpenAI?

**A:** The current implementation uses the OpenAI direct API. To use Azure OpenAI, the `AiAssistantService` would need to be updated to use the Azure OpenAI SDK with endpoint and key configuration. This is not currently supported out of the box.

---

## Summary Checklist

Before using the AI assistant, ensure the following:

- [ ] You have an OpenAI account with API access.
- [ ] You have added a payment method to your OpenAI account.
- [ ] You have generated an API key from the OpenAI dashboard.
- [ ] You have set the `OpenAI:ApiKey` user secret in the ApiService project.
- [ ] You have entered financial data (net worth, budget, savings goals) in the application.
- [ ] The application builds and runs without errors.
- [ ] The chat widget appears in the bottom-right corner when signed in.
