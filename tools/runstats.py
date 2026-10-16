"""Read a run's TensorBoard scalars and print them normalized per 1000 decisions.

    venv\\Scripts\\python.exe tools\\runstats.py terrain-04
    venv\\Scripts\\python.exe tools\\runstats.py terrain-04 --tags

Per-episode averages are confounded by MaxStep: changing episode length rescales
every stat at once, so raw numbers cannot be compared across that change. One
decision is DECISION_PERIOD physics steps, so episode length / DECISION_PERIOD is
the number of decisions the policy actually made.
"""

import argparse
import os
import sys

from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

DECISION_PERIOD = 5

# terrain-03 @10.5M (airplaneTerrain-10499902), MaxStep 5000 - the best artifact so far
BASELINE_NAME = "terrain-03@10.5M"
BASELINE_GATES_PER_1K = 0.997
BASELINE_CRASHES_PER_1K = 0.102

LENGTH_TAG = "Environment/Episode Length"
REWARD_TAG = "Environment/Cumulative Reward"
ENTROPY_TAG = "Policy/Entropy"

GATE_TAG_CANDIDATES = [
    "Task/CheckpointsPerEpisode",
    "Task/Checkpoints",
    "Task/GatesPerEpisode",
]
CRASH_TAG_CANDIDATES = [
    "Episodes/CrashRate",
    "Episodes/Crashes",
    "Task/CrashRate",
]


def load_scalars(run_dir):
    accumulator = EventAccumulator(run_dir, size_guidance={"scalars": 0})
    accumulator.Reload()
    scalars = {}
    for tag in accumulator.Tags()["scalars"]:
        by_step = {}
        for event in accumulator.Scalars(tag):
            by_step[event.step] = event.value
        scalars[tag] = by_step
    return scalars


def pick_tag(scalars, candidates):
    for candidate in candidates:
        if candidate in scalars:
            return candidate
    return None


def format_value(value, width=8, digits=3):
    if value is None:
        return "-".rjust(width)
    return ("%.*f" % (digits, value)).rjust(width)


def format_delta(value, baseline):
    if value is None:
        return "-".rjust(9)
    ratio = value / baseline * 100.0
    return ("%.0f%%" % ratio).rjust(9)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("run_id")
    parser.add_argument("--behavior", default="airplaneTerrain")
    parser.add_argument("--tags", action="store_true", help="list every scalar tag and exit")
    parser.add_argument("--tail", type=int, default=0, help="only show the last N summaries")
    args = parser.parse_args()

    run_dir = os.path.join("results", args.run_id, args.behavior)
    if not os.path.isdir(run_dir):
        sys.exit("no such run directory: " + run_dir)

    scalars = load_scalars(run_dir)

    if args.tags:
        for tag in sorted(scalars):
            print(tag)
        return

    if LENGTH_TAG not in scalars:
        sys.exit("missing '" + LENGTH_TAG + "' - nothing to normalize against")

    gate_tag = pick_tag(scalars, GATE_TAG_CANDIDATES)
    crash_tag = pick_tag(scalars, CRASH_TAG_CANDIDATES)
    if gate_tag is None:
        print("warning: no checkpoint tag found, tried " + ", ".join(GATE_TAG_CANDIDATES))
    if crash_tag is None:
        print("warning: no crash tag found, tried " + ", ".join(CRASH_TAG_CANDIDATES))

    steps = sorted(scalars[LENGTH_TAG])
    if args.tail > 0:
        steps = steps[-args.tail:]

    print("run " + args.run_id + "  baseline " + BASELINE_NAME
          + " (" + str(BASELINE_GATES_PER_1K) + " gates, "
          + str(BASELINE_CRASHES_PER_1K) + " crashes per 1000 decisions)")
    print("")
    header = ("      step" + "  decisions" + "  reward/1k" + " gates/1k" + "  vs base"
              + " crash/1k" + "  vs base" + "  entropy")
    print(header)

    for step in steps:
        episode_length = scalars[LENGTH_TAG][step]
        decisions = episode_length / DECISION_PERIOD
        if decisions <= 0.0:
            continue

        reward_per_1k = None
        if REWARD_TAG in scalars and step in scalars[REWARD_TAG]:
            reward_per_1k = scalars[REWARD_TAG][step] / decisions * 1000.0

        gates_per_1k = None
        if gate_tag is not None and step in scalars[gate_tag]:
            gates_per_1k = scalars[gate_tag][step] / decisions * 1000.0

        crashes_per_1k = None
        if crash_tag is not None and step in scalars[crash_tag]:
            crashes_per_1k = scalars[crash_tag][step] / decisions * 1000.0

        entropy = None
        if ENTROPY_TAG in scalars and step in scalars[ENTROPY_TAG]:
            entropy = scalars[ENTROPY_TAG][step]

        row = ("%10d" % step
               + "%11.0f" % decisions
               + format_value(reward_per_1k, 11, 2)
               + format_value(gates_per_1k, 9)
               + format_delta(gates_per_1k, BASELINE_GATES_PER_1K)
               + format_value(crashes_per_1k, 9)
               + format_delta(crashes_per_1k, BASELINE_CRASHES_PER_1K)
               + format_value(entropy, 9))
        print(row)

    print("")
    print("gates vs base above 100% beats the best artifact; crash vs base above 100% is worse.")
    print("if gates and crashes both fall toward zero the plane is loitering, not improving.")


if __name__ == "__main__":
    main()
