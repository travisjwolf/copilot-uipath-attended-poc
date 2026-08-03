# Repository Guidelines

## Project Structure & Module Organization

`src/AttendedLaunchBridge.Api/` contains the .NET 8 localhost launch bridge.
Keep HTTP models in `Contracts/`, business state in `Domain/`, interfaces and
coordination in `Application/`, and Robot-specific implementations in
`Infrastructure/`. Integration tests live in
`tests/AttendedLaunchBridge.Tests/`. Put operator scripts in `scripts/` and
architecture or setup notes in `docs/`. A future UiPath process should use its
own clearly named project directory rather than mixing XAML into the API.

## Build, Test, and Development Commands

- `dotnet restore .\CopilotUiPathAttendedPoc.sln` restores pinned dependencies.
- `dotnet build .\CopilotUiPathAttendedPoc.sln --no-restore` compiles all projects.
- `dotnet test .\CopilotUiPathAttendedPoc.sln --no-build` runs the xUnit suite.
- `dotnet run --project .\src\AttendedLaunchBridge.Api` starts the fake bridge
  on `http://127.0.0.1:5211` in Development.
- Use `uip rpa validate` and `uip rpa build .` for future UiPath workflow changes;
  inspect the installed package surface before authoring RobotAPI integration.

## Coding Style & Naming Conventions

Use four spaces in C# and two spaces in JSON, YAML, and Markdown. Nullable
reference types and implicit usings are enabled. Use `PascalCase` for public
types and members, `camelCase` for parameters and locals, and prefix interfaces
with `I`. Name tests `Method_Scenario_ExpectedResult`. Run `dotnet format
--verify-no-changes` before opening a pull request.

## Testing Guidelines

xUnit and `Microsoft.AspNetCore.Mvc.Testing` provide endpoint-level integration
tests. Add tests with every behavior change, especially authorization,
idempotency, status transitions, cancellation, expiration, and concurrency.
Use only synthetic messages and identities. Run the full build and test
commands before committing.

## Commit & Pull Request Guidelines

Use Conventional Commits, for example `feat: add fake robot job lifecycle` or
`fix: preserve cancelled job status`. Keep commits small and reviewable. Pull
requests should summarize behavior, link the issue, list validation commands,
and include screenshots only when attended desktop behavior changes.

## Security & Configuration

Never commit API keys, credentials, certificates, customer data, `bin/`, or
`obj/`. Store secrets in user secrets or environment variables. Keep the
bridge bound to localhost, expose only configured process aliases, and never
accept executable paths, XAML paths, release keys, or arbitrary process names.
