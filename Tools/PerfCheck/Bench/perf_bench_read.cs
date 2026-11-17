// Reads what perf_bench.cs left: "running: ..." while it goes, then the result JSON.
//   unity cmd --port <port> script eval --file Tools/PerfCheck/Bench/perf_bench_read.cs
var domain = System.AppDomain.CurrentDomain;
string status = domain.GetData("perfbench_status") as string ?? "never armed";
string result = domain.GetData("perfbench_result") as string;
return result != null && !status.StartsWith("running") ? result : "STATUS " + status;
