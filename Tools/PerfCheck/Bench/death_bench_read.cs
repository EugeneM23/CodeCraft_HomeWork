// Reads what death_bench.cs left: "running: ..." while it goes, then the result JSON.
//   unity cmd --project-path <worktree> script eval --file Tools/PerfCheck/Bench/death_bench_read.cs
var domain = System.AppDomain.CurrentDomain;
string status = domain.GetData("deathbench_status") as string ?? "never armed";
string result = domain.GetData("deathbench_result") as string;
return result != null && !status.StartsWith("running") ? result : "STATUS " + status;
