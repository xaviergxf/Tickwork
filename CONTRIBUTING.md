# Contributing to Tickwork

Issues and pull requests are welcome.

## Development setup

- The .NET SDK named in `global.json` (a .NET 11 SDK; it builds both `net10.0` and `net11.0`).
- Docker or Podman, for the tests that start PostgreSQL and SQL Server in containers.

```bash
dotnet build Tickwork.slnx
dotnet test Tickwork.slnx
```

## Guidelines

- The build has no warnings; keep it that way.
- Add a test for every fix and every feature, and see it fail before the change.
- Do not change the EF Core model: the database must stay the one TickerQ 10.4.0 created. The tests
  under `ModelPin` fail on any change to a table, column, index or stored enum value.
- Keep a pull request to one change.

## Reporting issues

Use [GitHub Issues](https://github.com/xaviergxf/Tickwork/issues), with the steps to reproduce, what
you expected and what happened.

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
