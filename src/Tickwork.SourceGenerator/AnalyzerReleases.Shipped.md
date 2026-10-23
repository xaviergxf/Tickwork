## Release 2.5.2

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
TQ001   | Tickwork.SourceGenerator | Error | Class should be public or internal to be used with [TickerFunction]
TQ002   | Tickwork.SourceGenerator | Error | Method should be public or internal to be used with [TickerFunction]
TQ003   | Tickwork.SourceGenerator | Error | Invalid cron expression
TQ004   | Tickwork.SourceGenerator | Error | Missing function name in [TickerFunction] attribute
TQ005   | Tickwork.SourceGenerator | Error | Duplicate function name across [TickerFunction] methods
TQ006   | Tickwork.SourceGenerator | Warning | Multiple constructors detected - first constructor will be used unless [TickworkConstructor] attribute is specified
TQ007   | Tickwork.SourceGenerator | Error | Abstract class contains [TickerFunction] methods
TQ008   | Tickwork.SourceGenerator | Error | Nested class contains [TickerFunction] methods - only allowed in top-level classes
TQ009   | Tickwork.SourceGenerator | Error | Invalid TickerFunction parameter - only TickerFunctionContext, TickerFunctionContext<T>, CancellationToken, or no parameters allowed
TQ010   | Tickwork.SourceGenerator | Error | Multiple constructors with [TickworkConstructor] attribute - only one constructor can be marked