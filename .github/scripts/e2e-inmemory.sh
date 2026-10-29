#!/usr/bin/env bash
# Runs the E2E suites the way CI's "E2E Browser Tests (Playwright)" job does (deploy-azure.yml), with no database:
# both apps start on the InMemory provider on free ports, the UI suite (npm run test:e2e) and the API suite
# (npm run test:api) run against them, and the apps stop. For the scheduled ticket agent (claude-ticket-agent.yml),
# whose runner has no SQL Server, and for a local run when the dev database is paused.
#
#   e2e-inmemory.sh [--no-build] [ui|api|all]
#     --no-build  skip the Release build (the CI gate: warnings are errors); dotnet run still builds Debug
#     ui|api|all  which suites to run (default: all)
#
# Needs the .NET SDK, the Node packages (npm ci) and Playwright's Chromium (npx playwright install chromium).
# The apps' output goes to .api.log and .web.log in the repository root; the last lines are printed when an app
# fails to start or a suite fails. Exit code: 0 when every suite passed.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

BUILD=true
SUITES=all
for arg in "$@"; do
  case "$arg" in
    --no-build) BUILD=false ;;
    ui|api|all) SUITES="$arg" ;;
    *) echo "usage: $0 [--no-build] [ui|api|all]" >&2; exit 2 ;;
  esac
done

API_PORT=$((RANDOM % 10000 + 10000))
WEB_PORT=$((RANDOM % 10000 + 10000))
API_URL="http://localhost:$API_PORT"
WEB_URL="http://localhost:$WEB_PORT"
API_PID=""
WEB_PID=""

stop_apps() {
  for pid in "$API_PID" "$WEB_PID"; do
    [ -n "$pid" ] && kill "$pid" 2>/dev/null || true
  done
  # dotnet run starts the app as a child; on Linux the parent's exit does not always take it along
  if command -v pkill > /dev/null 2>&1; then
    pkill -f "dotnet.*PersonalFinance.ApiService" 2>/dev/null || true
    pkill -f "dotnet.*PersonalFinance.Web" 2>/dev/null || true
  fi
}
trap stop_apps EXIT

# Waits for /health to answer 200: 50 tries, 3 seconds apart, as CI does.
wait_for() {
  local name="$1" url="$2" log="$3" i code
  for i in $(seq 1 50); do
    code=$(curl -s -S --connect-timeout 5 --max-time 10 -o /dev/null -w "%{http_code}" "$url/health" 2>/dev/null || echo "000")
    if [ "$code" = "200" ]; then
      echo "$name is healthy at $url (after $i tries)."
      return 0
    fi
    sleep 3
  done
  echo "::error::$name failed to start within timeout. Last 200 lines of $log:"
  tail -n 200 "$log" || true
  return 1
}

if [ "$BUILD" = "true" ]; then
  echo "== Building (Release, warnings as errors: the CI gate)"
  dotnet build PersonalFinance.sln --configuration Release -p:EnforceWarningsAsErrors=true || exit 1
fi

echo "== Starting ApiService on $API_URL (InMemory; logs -> .api.log)"
# OpenAI__ApiKey: a blank key counts as no key, so a user secret with a real key does not spend quota in E2E.
ASPNETCORE_ENVIRONMENT=Development DATABASE_PROVIDER=InMemory ConnectionStrings__DefaultConnection=InMemory:E2ETestApi \
  E2E_SEED_RESEARCH=true OpenAI__ApiKey=" " \
  Learning__ContentPath="$ROOT/PersonalFinance/tests/PersonalFinance.Tests/Learning/Fixtures/Content" \
  dotnet run --project PersonalFinance/src/PersonalFinance.ApiService --no-launch-profile \
    --environment Development --urls "http://0.0.0.0:$API_PORT" > .api.log 2>&1 &
API_PID=$!
wait_for ApiService "$API_URL" .api.log || exit 1

echo "== Starting Web on $WEB_URL (InMemory; logs -> .web.log)"
ASPNETCORE_ENVIRONMENT=Development DATABASE_PROVIDER=InMemory ConnectionStrings__DefaultConnection=InMemory:E2ETestWeb \
  ApiSettings__BaseUrl="$API_URL" E2E_DISABLE_EMAIL_CONFIRMATION=true \
  dotnet run --project PersonalFinance/src/PersonalFinance.Web --no-launch-profile \
    --environment Development --urls "http://0.0.0.0:$WEB_PORT" > .web.log 2>&1 &
WEB_PID=$!
wait_for Web "$WEB_URL" .web.log || exit 1

export WEB_URL API_URL DATABASE_PROVIDER=InMemory
failed=0
if [ "$SUITES" = "all" ] || [ "$SUITES" = "ui" ]; then
  echo "== UI suite (npm run test:e2e) against $WEB_URL"
  npm run test:e2e || failed=1
fi
if [ "$SUITES" = "all" ] || [ "$SUITES" = "api" ]; then
  echo "== API suite (npm run test:api) against $API_URL"
  npm run test:api || failed=1
fi

if [ "$failed" != "0" ]; then
  echo "::error::An E2E suite failed. Last 100 lines of .web.log:"
  tail -n 100 .web.log || true
  exit 1
fi
echo "OK: every E2E suite passed."
