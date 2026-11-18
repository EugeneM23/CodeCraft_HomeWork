// Reads what combat_bench.cs left: "STATUS running: ..." while it goes, then the result JSON.
//   unity cmd --project-path <worktree> script eval --file Tools/PerfCheck/Bench/horde_bench_read.cs
var domain = System.AppDomain.CurrentDomain;
string status = domain.GetData("combatbench_status") as string ?? "never armed";
string result = domain.GetData("combatbench_result") as string;
return result != null && !status.StartsWith("running") ? result : "STATUS " + status;
