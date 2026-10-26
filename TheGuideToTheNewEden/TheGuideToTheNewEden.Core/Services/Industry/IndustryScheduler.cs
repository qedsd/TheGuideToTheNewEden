using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;

namespace TheGuideToTheNewEden.Core.Services.Industry
{
    /// <summary>
    /// 按技能槽位 + 角色耗时把 BOM 作业排到角色/产线。
    /// 贪心列表调度（关键路径优先），并用多种优先级各跑一遍取较优结果。
    /// </summary>
    public static class IndustryScheduler
    {
        private const double Epsilon = 0.000001;

        public static IndustrySchedule Build(
            IndustryBuildPlan plan,
            IndustrySolverOptions options,
            IndustryScheduleObjective objective = IndustryScheduleObjective.Makespan,
            IReadOnlyDictionary<string, long>? pinnedCharacters = null)
        {
            var result = new IndustrySchedule
            {
                Objective = objective,
                GeneratedAt = DateTime.Now,
            };
            if (plan?.Targets is null || plan.Targets.Count == 0)
            {
                return result;
            }

            var targetIds = new HashSet<int>(plan.Targets.Where(p => p != null).Select(p => p.TypeID));
            var jobs = new Dictionary<string, JobWork>(StringComparer.Ordinal);
            foreach (var target in plan.Targets)
            {
                Collect(target, jobs, targetIds);
            }

            foreach (var job in jobs.Values)
            {
                job.DependencyKeys.RemoveWhere(key => !jobs.ContainsKey(key) || key == job.Key);
                job.Dependents.Clear();
            }
            foreach (var job in jobs.Values)
            {
                foreach (var dependencyKey in job.DependencyKeys)
                {
                    jobs[dependencyKey].Dependents.Add(job);
                }
            }

            foreach (var job in jobs.Values)
            {
                job.Lookup = jobs;
            }
            foreach (var job in jobs.Values)
            {
                ComputeDepth(job, new HashSet<string>(StringComparer.Ordinal));
            }
            foreach (var job in jobs.Values)
            {
                ComputeDownstream(job, new HashSet<string>(StringComparer.Ordinal));
            }

            var profiles = NormalizeProfiles(options.CharacterProfiles);
            RunSnapshot? best = null;
            foreach (var order in new[] { "critical", "longest", "depth" })
            {
                var candidate = Run(profiles, jobs, order, objective, pinnedCharacters, options.InProgressJobDetails);
                if (candidate == null) continue;
                if (IsBetter(candidate, best, objective))
                {
                    best = candidate;
                }
            }

            if (best is null || (best.Assignments.Count == 0 && best.ActiveAssignments.Count == 0))
            {
                return result;
            }

            return BuildResult(jobs, profiles, best, objective);
        }

        private static void Collect(
            IndustryBuildNode node,
            Dictionary<string, JobWork> jobs,
            HashSet<int> targetIds)
        {
            if (node is null) return;

            if (node.Decision == IndustryDecision.Build
                && node.Runs > 0)
            {
                var key = JobKey(node.TypeID, node.ActivityID);
                if (!jobs.TryGetValue(key, out var job))
                {
                    job = new JobWork
                    {
                        Key = key,
                        TypeId = node.TypeID,
                        TypeName = node.TypeName,
                        ActivityId = node.ActivityID,
                        IsInvention = node.IsInvention,
                        IsTarget = targetIds.Contains(node.TypeID),
                    };
                    jobs[key] = job;
                }

                job.Runs += node.Runs;
                job.QuantityProduced += node.QuantityProduced;
                job.JobCost += node.JobCost;
                job.MaterialCost += Math.Max(0, node.BuildCost - node.JobCost);
                job.BaseHours += Math.Max(0, node.JobTimeSeconds) / 3600.0;
                job.IsTarget |= targetIds.Contains(node.TypeID);
                job.IsInvention |= node.IsInvention;

                if (node.Inputs != null)
                {
                    foreach (var input in node.Inputs)
                    {
                        if (input is null) continue;
                        var state = InputState(input);
                        var materialKey = $"{input.TypeID}:{state}";
                        if (job.Inputs.TryGetValue(materialKey, out var material))
                        {
                            material.Quantity += input.QuantityNeeded;
                        }
                        else
                        {
                            job.Inputs[materialKey] = new IndustryScheduleMaterial
                            {
                                TypeId = input.TypeID,
                                Name = string.IsNullOrWhiteSpace(input.TypeName) ? "#" + input.TypeID : input.TypeName,
                                Quantity = input.QuantityNeeded,
                                StateKey = state,
                                StateText = StateText(state),
                            };
                        }

                        if (input.Decision == IndustryDecision.Build
                            && input.Runs > 0)
                        {
                            job.DependencyKeys.Add(JobKey(input.TypeID, input.ActivityID));
                        }
                    }
                }

                var outputState = job.IsTarget && node.ActivityID != 8 ? "target" : "downstream";
                var outputKey = $"{node.TypeID}:{outputState}";
                if (job.Outputs.TryGetValue(outputKey, out var output))
                {
                    output.Quantity += node.QuantityProduced;
                }
                else
                {
                    job.Outputs[outputKey] = new IndustryScheduleMaterial
                    {
                        TypeId = node.TypeID,
                        Name = string.IsNullOrWhiteSpace(node.TypeName) ? "#" + node.TypeID : node.TypeName,
                        Quantity = node.QuantityProduced,
                        StateKey = outputState,
                        StateText = StateText(outputState),
                    };
                }
            }

            if (node.Inputs != null)
            {
                foreach (var child in node.Inputs)
                {
                    Collect(child, jobs, targetIds);
                }
            }
        }

        private static void ComputeDepth(JobWork job, HashSet<string> visiting)
        {
            if (job.DepthComputed) return;
            if (!visiting.Add(job.Key))
            {
                job.Depth = 0;
                job.DepthComputed = true;
                return;
            }

            int depth = 0;
            foreach (var dependencyKey in job.DependencyKeys)
            {
                if (job.Lookup is null || !job.Lookup.TryGetValue(dependencyKey, out var dependency)) continue;
                ComputeDepth(dependency, visiting);
                depth = Math.Max(depth, dependency.Depth + 1);
            }
            job.Depth = depth;
            job.DepthComputed = true;
            visiting.Remove(job.Key);
        }

        private static void ComputeDownstream(JobWork job, HashSet<string> visiting)
        {
            if (job.DownstreamComputed) return;
            if (!visiting.Add(job.Key))
            {
                job.DownstreamHours = job.BaseHours;
                job.DownstreamComputed = true;
                return;
            }

            double downstream = 0;
            foreach (var dependent in job.Dependents)
            {
                ComputeDownstream(dependent, visiting);
                downstream = Math.Max(downstream, dependent.DownstreamHours);
            }
            job.DownstreamHours = Math.Max(0, job.BaseHours) + downstream;
            job.DownstreamComputed = true;
            visiting.Remove(job.Key);
        }

        private static RunSnapshot? Run(
            List<IndustryCharacterProfile> profiles,
            Dictionary<string, JobWork> jobs,
            string order,
            IndustryScheduleObjective objective,
            IReadOnlyDictionary<string, long>? pinnedCharacters,
            IReadOnlyList<IndustryInProgressJob>? activeJobs)
        {
            var states = profiles.Select(p => new ProfileState
            {
                Profile = p,
                ManufacturingFree = new double[Math.Max(0, p.ManufacturingSlots)],
                InventionFree = new double[Math.Max(0, p.InventionSlots)],
                ReactionFree = new double[Math.Max(0, p.ReactionSlots)],
            }).ToList();

            var activeAssignments = ReserveInProgress(states, activeJobs);
            var assignments = new Dictionary<string, Assignment>(StringComparer.Ordinal);
            var remaining = jobs.Values.ToDictionary(p => p.Key, p => p.DependencyKeys.Count, StringComparer.Ordinal);
            var ready = new List<JobWork>();
            foreach (var job in jobs.Values)
            {
                job.Lookup = jobs;
                if (remaining[job.Key] == 0) ready.Add(job);
            }

            while (ready.Count > 0)
            {
                var job = PickReady(ready, order);
                ready.Remove(job);

                var readyTime = 0.0;
                foreach (var dependencyKey in job.DependencyKeys)
                {
                    if (assignments.TryGetValue(dependencyKey, out var dependency))
                    {
                        readyTime = Math.Max(readyTime, dependency.End);
                    }
                }

                var pinned = pinnedCharacters != null && pinnedCharacters.TryGetValue(job.Key, out var pinnedId)
                    ? pinnedId
                    : 0;
                var best = FindBest(job, states, readyTime, objective, pinned);
                if (best is null)
                {
                    continue;
                }

                assignments[job.Key] = best;
                var state = states.First(p => p.Profile.CharacterId == best.CharacterId && ReferenceEquals(p.Profile, best.Profile));
                SetFree(state, job.ActivityId, best.LineIndex, best.End);

                foreach (var dependent in job.Dependents)
                {
                    remaining[dependent.Key]--;
                    if (remaining[dependent.Key] == 0)
                    {
                        ready.Add(dependent);
                    }
                }
            }

            if (assignments.Count == 0 && activeAssignments.Count == 0)
            {
                return null;
            }

            var makespan = Math.Max(
                assignments.Values.Select(p => p.End).DefaultIfEmpty(0).Max(),
                activeAssignments.Select(p => p.End).DefaultIfEmpty(0).Max());
            var cost = 0.0;
            foreach (var pair in assignments)
            {
                var profile = profiles.FirstOrDefault(p => p.CharacterId == pair.Value.CharacterId);
                cost += jobs[pair.Key].JobCost * (profile?.CostMultiplier ?? 1.0);
            }

            return new RunSnapshot
            {
                Assignments = assignments,
                ActiveAssignments = activeAssignments,
                Makespan = makespan,
                Cost = cost,
                Complete = assignments.Count == jobs.Count,
            };
        }

        private static Assignment? FindBest(
            JobWork job,
            List<ProfileState> states,
            double readyTime,
            IndustryScheduleObjective objective,
            long pinnedCharacterId)
        {
            Assignment? best = null;
            var bestScore = double.MaxValue;

            foreach (var state in states)
            {
                if (pinnedCharacterId != 0 && state.Profile.CharacterId != pinnedCharacterId) continue;
                var free = GetFree(state, job.ActivityId);
                if (free.Length == 0) continue;

                var multiplier = state.Profile.TimeMultiplierForActivity(job.ActivityId);
                var duration = Math.Max(0.01, job.BaseHours * multiplier);
                for (int i = 0; i < free.Length; i++)
                {
                    var start = Math.Max(readyTime, free[i]);
                    var end = start + duration;

                    double score;
                    switch (objective)
                    {
                        case IndustryScheduleObjective.Cost:
                            score = job.JobCost * Math.Max(0.01, state.Profile.CostMultiplier) + end * 0.000001;
                            break;
                        case IndustryScheduleObjective.Balanced:
                            score = end + job.JobCost * Math.Max(0.01, state.Profile.CostMultiplier) * 0.000001;
                            break;
                        default:
                            score = end;
                            break;
                    }

                    if (score < bestScore - Epsilon)
                    {
                        bestScore = score;
                        best = new Assignment
                        {
                            CharacterId = state.Profile.CharacterId,
                            CharacterName = state.Profile.CharacterName,
                            Profile = state.Profile,
                            LineKind = LineKind(job.ActivityId),
                            LineId = LineId(job.ActivityId, i),
                            LineIndex = i,
                            Start = start,
                            End = end,
                            Duration = duration,
                            Multiplier = multiplier,
                            IsPinned = pinnedCharacterId != 0,
                        };
                    }
                }
            }

            return best;
        }

        private static JobWork PickReady(List<JobWork> ready, string order)
        {
            JobWork best = ready[0];
            foreach (var job in ready)
            {
                if (order switch
                {
                    "longest" => job.BaseHours > best.BaseHours,
                    "depth" => job.Depth > best.Depth,
                    _ => job.DownstreamHours > best.DownstreamHours,
                })
                {
                    best = job;
                }
            }
            return best;
        }

        private static bool IsBetter(RunSnapshot candidate, RunSnapshot? best, IndustryScheduleObjective objective)
        {
            if (best is null) return true;
            if (candidate.Complete != best.Complete) return candidate.Complete;
            if (objective == IndustryScheduleObjective.Cost)
            {
                if (candidate.Cost < best.Cost - Epsilon) return true;
                if (candidate.Cost > best.Cost + Epsilon) return false;
                return candidate.Makespan < best.Makespan - Epsilon;
            }
            if (candidate.Makespan < best.Makespan - Epsilon) return true;
            if (candidate.Makespan > best.Makespan + Epsilon) return false;
            return candidate.Cost < best.Cost - Epsilon;
        }

        private static IndustrySchedule BuildResult(
            Dictionary<string, JobWork> jobs,
            List<IndustryCharacterProfile> profiles,
            RunSnapshot best,
            IndustryScheduleObjective objective)
        {
            var result = new IndustrySchedule
            {
                Objective = objective,
                GeneratedAt = DateTime.Now,
                MakespanHours = best.Makespan,
                CriticalJobKeys = new List<string>(),
            };

            foreach (var pair in jobs.OrderBy(p => p.Value.Depth).ThenBy(p => p.Value.TypeName))
            {
                var job = pair.Value;
                if (!best.Assignments.TryGetValue(job.Key, out var assignment)) continue;

                // 阶段用语言无关代码；WPF 侧按 code 查语言键显示。
                var stage = job.ActivityId == 8 ? "invention"
                    : job.ActivityId == 11 ? "reaction"
                    : job.ActivityId == 100 ? "planetary"
                    : job.IsTarget ? "target"
                    : "component";

                var resultJob = new IndustryScheduleJob
                {
                    Key = job.Key,
                    TypeId = job.TypeId,
                    TypeName = job.TypeName,
                    ActivityId = job.ActivityId,
                    ActivityKey = ActivityKey(job.ActivityId),
                    ActivityText = ActivityText(job.ActivityId),
                    IsInvention = job.IsInvention,
                    IsTarget = job.IsTarget,
                    Stage = stage,
                    Runs = job.Runs,
                    QuantityProduced = job.QuantityProduced,
                    BaseHours = job.BaseHours,
                    DurationHours = assignment.Duration,
                    StartHours = assignment.Start,
                    EndHours = assignment.End,
                    CharacterId = assignment.CharacterId,
                    CharacterName = assignment.CharacterName,
                    LineId = assignment.LineId,
                    LineKind = assignment.LineKind,
                    LineIndex = assignment.LineIndex,
                    TimeMultiplier = assignment.Multiplier,
                    IsPinned = assignment.IsPinned,
                    JobCost = job.JobCost,
                    MaterialCost = job.MaterialCost,
                    Inputs = job.Inputs.Values.OrderBy(p => StateOrder(p.StateKey)).ThenBy(p => p.Name).ToList(),
                    Outputs = job.Outputs.Values.OrderBy(p => p.Name).ToList(),
                    Dependencies = job.DependencyKeys.OrderBy(p => p).ToList(),
                    Dependents = job.Dependents.Select(p => p.Key).OrderBy(p => p).ToList(),
                };
                result.Jobs.Add(resultJob);
            }

            foreach (var active in best.ActiveAssignments)
            {
                var source = active.Job;
                result.Jobs.Add(new IndustryScheduleJob
                {
                    Key = "active:" + source.JobId,
                    TypeId = source.ProductTypeId,
                    TypeName = source.ProductName,
                    ActivityId = source.ActivityId,
                    ActivityKey = ActivityKey(source.ActivityId),
                    ActivityText = ActivityText(source.ActivityId),
                    IsInvention = source.IsInvention,
                    IsTarget = false,
                    IsInProgress = true,
                    Stage = "inprogress",
                    Runs = source.Runs,
                    QuantityProduced = source.QuantityProduced,
                    BaseHours = active.Duration,
                    DurationHours = active.Duration,
                    StartHours = active.Start,
                    EndHours = active.End,
                    CharacterId = active.CharacterId,
                    CharacterName = active.CharacterName,
                    LineId = active.LineId,
                    LineKind = active.LineKind,
                    LineIndex = active.LineIndex,
                    TimeMultiplier = 1.0,
                    JobCost = 0,
                    MaterialCost = 0,
                    Outputs = new List<IndustryScheduleMaterial>
                    {
                        new IndustryScheduleMaterial
                        {
                            TypeId = source.ProductTypeId,
                            Name = source.ProductName,
                            Quantity = source.QuantityProduced,
                            StateKey = "inprogress",
                            StateText = "inprogress",
                        },
                    },
                });
            }

            var byKey = result.Jobs.ToDictionary(p => p.Key, StringComparer.Ordinal);
            foreach (var job in result.Jobs)
            {
                var latestEnd = LatestEnd(job, byKey, result.MakespanHours, new HashSet<string>(StringComparer.Ordinal));
                job.IsCritical = latestEnd - job.EndHours <= 0.001;
                if (job.IsCritical)
                {
                    result.CriticalJobKeys.Add(job.Key);
                }
            }

            foreach (var profile in profiles)
            {
                var character = new IndustryScheduleCharacter
                {
                    CharacterId = profile.CharacterId,
                    CharacterName = profile.CharacterName,
                    ManufacturingSlots = profile.ManufacturingSlots,
                    InventionSlots = profile.InventionSlots,
                    ReactionSlots = profile.ReactionSlots,
                    ManufacturingTimeMultiplier = profile.ManufacturingTimeMultiplier,
                    ReactionTimeMultiplier = profile.ReactionTimeMultiplier,
                    InventionTimeMultiplier = profile.InventionTimeMultiplier,
                    Source = profile.Source,
                    SkillsLoaded = profile.SkillsLoaded,
                };

                for (int i = 0; i < profile.ManufacturingSlots; i++)
                {
                    character.Lines.Add(MakeLine("M", "manufacturing", i));
                }
                for (int i = 0; i < profile.InventionSlots; i++)
                {
                    character.Lines.Add(MakeLine("I", "invention", i));
                }
                for (int i = 0; i < profile.ReactionSlots; i++)
                {
                    character.Lines.Add(MakeLine("R", "reaction", i));
                }
                if (result.Jobs.Any(p => p.CharacterId == profile.CharacterId && p.ActivityId == 100))
                {
                    character.Lines.Add(MakeLine("P", "planetary", 0));
                }

                foreach (var job in result.Jobs.Where(p => p.CharacterId == profile.CharacterId))
                {
                    var line = character.Lines.FirstOrDefault(p => p.Id == job.LineId);
                    if (line is null) continue;
                    line.JobKeys.Add(job.Key);
                    line.BusyHours += job.DurationHours;
                    character.UsedSlotHours += job.DurationHours;
                }

                character.AvailableSlotHours = Math.Max(0, character.TotalSlots) * result.MakespanHours;
                character.UtilizationPercent = character.AvailableSlotHours > 0
                    ? Math.Clamp(character.UsedSlotHours / character.AvailableSlotHours * 100.0, 0, 100)
                    : 0;
                result.Characters.Add(character);
            }

            result.TotalJobCost = result.Jobs.Sum(p => p.JobCost);
            result.TotalMaterialCost = result.Jobs.Sum(p => p.MaterialCost);
            return result;
        }

        private static double LatestEnd(
            IndustryScheduleJob job,
            Dictionary<string, IndustryScheduleJob> byKey,
            double makespan,
            HashSet<string> visiting)
        {
            if (job.Dependents.Count == 0) return makespan;
            if (!visiting.Add(job.Key)) return makespan;

            double latest = double.MaxValue;
            foreach (var dependentKey in job.Dependents)
            {
                if (!byKey.TryGetValue(dependentKey, out var dependent)) continue;
                latest = Math.Min(latest, dependent.StartHours);
                LatestEnd(dependent, byKey, makespan, visiting);
            }
            visiting.Remove(job.Key);
            return latest == double.MaxValue ? makespan : latest;
        }

        private static IndustryScheduleLine MakeLine(string prefix, string kind, int index)
        {
            return new IndustryScheduleLine
            {
                Id = prefix + (index + 1),
                Kind = kind,
                Index = index + 1,
            };
        }

        private static List<ActiveAssignment> ReserveInProgress(
            List<ProfileState> states,
            IReadOnlyList<IndustryInProgressJob>? activeJobs)
        {
            var result = new List<ActiveAssignment>();
            if (activeJobs is null || activeJobs.Count == 0) return result;

            var nextIndex = new Dictionary<(long CharacterId, int ActivityId), int>();
            foreach (var job in activeJobs.OrderBy(p => p.EndDate))
            {
                var state = states.FirstOrDefault(p => p.Profile.CharacterId == job.CharacterId);
                if (state is null) continue;
                var free = GetFree(state, job.ActivityId);
                if (free.Length == 0) continue;

                var key = (job.CharacterId, job.ActivityId);
                var index = nextIndex.TryGetValue(key, out var used) ? used : 0;
                index %= free.Length;
                nextIndex[key] = index + 1;

                var duration = Math.Max(0.01, job.RemainingHours);
                free[index] = Math.Max(free[index], duration);
                result.Add(new ActiveAssignment
                {
                    Job = job,
                    CharacterId = job.CharacterId,
                    CharacterName = string.IsNullOrWhiteSpace(job.CharacterName)
                        ? state.Profile.CharacterName
                        : job.CharacterName,
                    Profile = state.Profile,
                    LineId = LineId(job.ActivityId, index),
                    LineKind = LineKind(job.ActivityId),
                    LineIndex = index,
                    Start = 0,
                    End = duration,
                    Duration = duration,
                });
            }
            return result;
        }

        private static List<IndustryCharacterProfile> NormalizeProfiles(IEnumerable<IndustryCharacterProfile>? profiles)
        {
            var list = new List<IndustryCharacterProfile>();
            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    if (profile is null) continue;
                    profile.ManufacturingSlots = Math.Clamp(profile.ManufacturingSlots, 0, 11);
                    profile.InventionSlots = Math.Clamp(profile.InventionSlots, 0, 11);
                    profile.ReactionSlots = Math.Clamp(profile.ReactionSlots, 0, 11);
                    profile.ManufacturingTimeMultiplier = Math.Clamp(profile.ManufacturingTimeMultiplier, 0.1, 2.0);
                    profile.ReactionTimeMultiplier = Math.Clamp(profile.ReactionTimeMultiplier, 0.1, 2.0);
                    profile.InventionTimeMultiplier = Math.Clamp(profile.InventionTimeMultiplier, 0.1, 2.0);
                    if (string.IsNullOrWhiteSpace(profile.CharacterName))
                    {
                        profile.CharacterName = profile.CharacterId != 0 ? "#" + profile.CharacterId : "auto:default";
                    }
                    list.Add(profile);
                }
            }

            if (list.Count == 0)
            {
                list.Add(new IndustryCharacterProfile
                {
                    CharacterId = 0,
                    CharacterName = "auto:default",
                    ManufacturingSlots = 11,
                    InventionSlots = 11,
                    ReactionSlots = 11,
                    Source = "default",
                });
            }
            return list;
        }

        private static double[] GetFree(ProfileState state, int activityId)
        {
            return activityId switch
            {
                8 => state.InventionFree,
                11 => state.ReactionFree,
                100 => state.PlanetFree,
                _ => state.ManufacturingFree,
            };
        }

        private static void SetFree(ProfileState state, int activityId, int index, double end)
        {
            var free = GetFree(state, activityId);
            if (index >= 0 && index < free.Length) free[index] = end;
        }

        private static string LineKind(int activityId)
        {
            return activityId switch
            {
                8 => "invention",
                11 => "reaction",
                100 => "planetary",
                _ => "manufacturing",
            };
        }

        private static string LineId(int activityId, int index)
        {
            return activityId switch
            {
                8 => "I" + (index + 1),
                11 => "R" + (index + 1),
                100 => "P" + (index + 1),
                _ => "M" + (index + 1),
            };
        }

        private static string JobKey(int typeId, int activityId) => typeId + ":" + activityId;

        private static string InputState(IndustryBuildNode input)
        {
            if (input.Decision == IndustryDecision.Buy)
            {
                return input.QuantityToBuy > 0 ? "buy" : "stock";
            }
            if (input.Decision == IndustryDecision.Build)
            {
                return input.Runs > 0 ? "upstream" : "stock";
            }
            return "upstream";
        }

        private static string StateText(string state) => state;

        private static int StateOrder(string state)
        {
            return state switch
            {
                "buy" => 0,
                "stock" => 1,
                "upstream" => 2,
                "target" => 0,
                "downstream" => 1,
                _ => 9,
            };
        }

        private static string ActivityKey(int activityId)
        {
            return activityId switch
            {
                8 => "invention",
                11 => "reaction",
                100 => "planetary",
                _ => "mfg",
            };
        }

        /// <summary>活动代码（与 <see cref="ActivityKey"/> 相同）；显示文案由 UI 层按语言生成。</summary>
        private static string ActivityText(int activityId) => ActivityKey(activityId);

        private sealed class JobWork
        {
            public string Key { get; set; } = string.Empty;
            public int TypeId { get; set; }
            public string TypeName { get; set; } = string.Empty;
            public int ActivityId { get; set; }
            public bool IsInvention { get; set; }
            public bool IsTarget { get; set; }
            public int Runs { get; set; }
            public long QuantityProduced { get; set; }
            public double BaseHours { get; set; }
            public double JobCost { get; set; }
            public double MaterialCost { get; set; }

            public Dictionary<string, IndustryScheduleMaterial> Inputs { get; } = new Dictionary<string, IndustryScheduleMaterial>(StringComparer.Ordinal);
            public Dictionary<string, IndustryScheduleMaterial> Outputs { get; } = new Dictionary<string, IndustryScheduleMaterial>(StringComparer.Ordinal);
            public HashSet<string> DependencyKeys { get; } = new HashSet<string>(StringComparer.Ordinal);
            public List<JobWork> Dependents { get; } = new List<JobWork>();

            public int Depth { get; set; }
            public bool DepthComputed { get; set; }
            public double DownstreamHours { get; set; }
            public bool DownstreamComputed { get; set; }
            public Dictionary<string, JobWork>? Lookup { get; set; }
        }

        private sealed class Assignment
        {
            public long CharacterId { get; set; }
            public string CharacterName { get; set; } = string.Empty;
            public IndustryCharacterProfile Profile { get; set; } = new IndustryCharacterProfile();
            public string LineId { get; set; } = string.Empty;
            public string LineKind { get; set; } = string.Empty;
            public int LineIndex { get; set; }
            public double Start { get; set; }
            public double End { get; set; }
            public double Duration { get; set; }
            public double Multiplier { get; set; }
            public bool IsPinned { get; set; }
        }

        private sealed class ActiveAssignment
        {
            public IndustryInProgressJob Job { get; set; } = new IndustryInProgressJob();
            public long CharacterId { get; set; }
            public string CharacterName { get; set; } = string.Empty;
            public IndustryCharacterProfile Profile { get; set; } = new IndustryCharacterProfile();
            public string LineId { get; set; } = string.Empty;
            public string LineKind { get; set; } = string.Empty;
            public int LineIndex { get; set; }
            public double Start { get; set; }
            public double End { get; set; }
            public double Duration { get; set; }
        }

        private sealed class RunSnapshot
        {
            public Dictionary<string, Assignment> Assignments { get; set; } = new Dictionary<string, Assignment>(StringComparer.Ordinal);
            public List<ActiveAssignment> ActiveAssignments { get; set; } = new List<ActiveAssignment>();
            public double Makespan { get; set; }
            public double Cost { get; set; }
            public bool Complete { get; set; }
        }

        private sealed class ProfileState
        {
            public IndustryCharacterProfile Profile { get; set; } = new IndustryCharacterProfile();
            public double[] ManufacturingFree { get; set; } = Array.Empty<double>();
            public double[] InventionFree { get; set; } = Array.Empty<double>();
            public double[] ReactionFree { get; set; } = Array.Empty<double>();
            public double[] PlanetFree { get; set; } = new double[1];
        }
    }
}
