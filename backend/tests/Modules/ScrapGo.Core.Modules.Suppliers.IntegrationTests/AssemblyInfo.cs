// Fixtures configure the host through process-wide environment variables
// (read by Program.cs before the host is built), so scenarios must not run
// concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
