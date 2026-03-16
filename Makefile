.PHONY: build run test clean docker-up docker-down docker-build docker-run migrate restore logs

# --- .NET commands ---

restore:
	dotnet restore

build: restore
	dotnet build --no-restore

run:
	dotnet run --project src/ReleasePilot.Api

run-https:
	dotnet run --project src/ReleasePilot.Api --launch-profile https

test:
	dotnet test

clean:
	dotnet clean
	rm -rf src/*/bin src/*/obj tests/*/bin tests/*/obj

migrate:
	dotnet ef migrations add $(name) --project src/ReleasePilot.Infrastructure --startup-project src/ReleasePilot.Api

# --- Docker commands ---

docker-up:
	docker compose up -d

docker-down:
	docker compose down

docker-build:
	docker compose build

docker-run: docker-build
	docker compose up -d

docker-logs:
	docker compose logs -f api

docker-clean:
	docker compose down -v

# --- Utilities ---

logs:
	@echo "Logs are stored in the 'logs' directory of the running application."
	@echo "For Docker: docker compose logs -f api"
	@echo "For local: check src/ReleasePilot.Api/bin/Debug/net10.0/logs/"

health:
	curl -s http://localhost:5180/health | python -m json.tool 2>/dev/null || curl -s http://localhost:5180/health

swagger:
	@echo "Open http://localhost:5180/swagger in your browser"
