#!/usr/bin/env python3
"""
Parse EduXplore game logs (FRTest_logs/*.json) into a PLAYER-based summary —
every round and recognition attempt is grouped by the resolved player NAME across
all games/sessions (not by game or file), so you can evaluate one specific person.

Handles the three log schemas currently produced by the project:

  1. SessionLog   ("<timestamp>_<GameName>.json", e.g. *_TestTongHop.json)
     -> written by Assets/Game/Scripts/UIScripts/_TestTongHop/Logger/GameLogger.cs
     -> {gameName, playerLeft, playerRight, rounds:[{..., clicks:[...]}]}

  2. GameLog      ("<timestamp>_<GameName>.json")
     -> written by Assets/Game/Scripts/UIScripts/Common/PlayerRecognitionService.cs
     -> {entries:[{eventType:"recognition"|"round", slot, name, ...}]}

  3. Recognition  ("Recognition_<GameName>_<timestamp>.json")
     -> older/simpler recognition-only log
     -> {entries:[{slot, name, recognized, elapsedSec}]}  (no eventType/round data)

Usage:
    python Tools/parse_game_logs.py [log_dir] [-o out.csv]

Defaults to ./FRTest_logs and writes Tools/game_log_summary.csv.
"""
from __future__ import annotations

import argparse
import csv
import io
import json
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

# Windows consoles default to cp1252, which can't print Vietnamese names (Đạt, Hải, ...).
if sys.platform == "win32":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")


def detect_schema(data: dict) -> str:
    if "entries" in data:
        entries = data["entries"]
        if entries and "eventType" in entries[0]:
            return "gamelog"
        return "recognition"
    if "rounds" in data:
        return "sessionlog"
    return "unknown"


def mean(xs: list[float]) -> float | None:
    xs = [x for x in xs if x is not None]
    return round(sum(xs) / len(xs), 2) if xs else None


def r2(x: float | None) -> float | None:
    """Round to 2dp — Unity's float32 values pick up binary-repr noise (5.0799999...)."""
    return round(x, 2) if x is not None else None


def pct(n: int, d: int) -> float | None:
    return round(100.0 * n / d, 1) if d else None


def parse_gamelog(data: dict, source: str) -> list[dict[str, Any]]:
    """entries with eventType 'recognition' / 'round', keyed by slot (left/right)."""
    by_slot: dict[str, dict[str, list]] = defaultdict(
        lambda: {"recog": [], "rounds": []}
    )
    game = None
    for e in data["entries"]:
        game = e.get("game", game)
        slot = e.get("slot", "?")
        if e["eventType"] == "recognition":
            by_slot[slot]["recog"].append(e)
        else:
            by_slot[slot]["rounds"].append(e)

    rows = []
    for slot, buckets in by_slot.items():
        recog = buckets["recog"]
        rounds = buckets["rounds"]
        player_name = rounds[-1]["name"] if rounds else (recog[-1]["name"] if recog else "?")
        correct = sum(1 for r in rounds if r.get("correct"))
        rows.append(
            {
                "source_file": source,
                "game": game,
                "player": player_name,
                "slot": slot,
                "rounds_played": len(rounds),
                "rounds_correct": correct,
                "accuracy_pct": pct(correct, len(rounds)),
                "avg_answer_time_sec": mean([r.get("answerTimeSec") for r in rounds]),
                "recognition_attempts": len(recog),
                "recognition_success_pct": pct(
                    sum(1 for r in recog if r.get("recognized")), len(recog)
                ),
                "avg_recognize_elapsed_sec": mean(
                    [r.get("recognizeElapsedSec") for r in recog]
                ),
            }
        )
    return rows


def parse_recognition(data: dict, source: str) -> list[dict[str, Any]]:
    """entries with just slot/name/recognized/elapsedSec (no round/answer data)."""
    by_slot: dict[str, list] = defaultdict(list)
    game = None
    for e in data["entries"]:
        game = e.get("game", game)
        by_slot[e.get("slot", "?")].append(e)

    rows = []
    for slot, recog in by_slot.items():
        player_name = recog[-1]["name"] if recog else "?"
        rows.append(
            {
                "source_file": source,
                "game": game,
                "player": player_name,
                "slot": slot,
                "rounds_played": None,
                "rounds_correct": None,
                "accuracy_pct": None,
                "avg_answer_time_sec": None,
                "recognition_attempts": len(recog),
                "recognition_success_pct": pct(
                    sum(1 for r in recog if r.get("recognized")), len(recog)
                ),
                "avg_recognize_elapsed_sec": mean([r.get("elapsedSec") for r in recog]),
            }
        )
    return rows


def parse_sessionlog(data: dict, source: str) -> list[dict[str, Any]]:
    """{gameName, playerLeft, playerRight, rounds:[{..., clicks:[...]}]}"""
    game = data.get("gameName")
    sides = {
        "Left": {"name": data.get("playerLeft"), "wins": 0, "recognized": 0, "times": []},
        "Right": {"name": data.get("playerRight"), "wins": 0, "recognized": 0, "times": []},
    }
    rounds = data.get("rounds", [])
    for r in rounds:
        if r.get("playerLeftRecognized"):
            sides["Left"]["recognized"] += 1
        if r.get("playerRightRecognized"):
            sides["Right"]["recognized"] += 1
        if r.get("isCorrect") and r.get("winnerName"):
            for side in sides.values():
                if side["name"] == r["winnerName"]:
                    side["wins"] += 1
        sides["Left"]["times"].append(r.get("responseTimeSeconds"))
        sides["Right"]["times"].append(r.get("responseTimeSeconds"))

    total = len(rounds)
    rows = []
    for slot, side in sides.items():
        rows.append(
            {
                "source_file": source,
                "game": game,
                "player": side["name"],
                "slot": slot.lower(),
                "rounds_played": total,
                "rounds_correct": side["wins"],
                "accuracy_pct": pct(side["wins"], total),
                "avg_answer_time_sec": mean(side["times"]),
                "recognition_attempts": total,
                "recognition_success_pct": pct(side["recognized"], total),
                "avg_recognize_elapsed_sec": None,
            }
        )
    return rows


def parse_file(path: Path) -> list[dict[str, Any]]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as e:
        print(f"  ! skipped {path.name}: {e}", file=sys.stderr)
        return []

    schema = detect_schema(data)
    if schema == "gamelog":
        return parse_gamelog(data, path.name)
    if schema == "recognition":
        return parse_recognition(data, path.name)
    if schema == "sessionlog":
        return parse_sessionlog(data, path.name)
    print(f"  ! unknown schema in {path.name}", file=sys.stderr)
    return []


# ── Player-based aggregation (ignores game/slot — groups every round & recognition ─────────
# attempt by the resolved player NAME, across every session file, to evaluate that person) ──


def collect_round_records(files: list[Path]) -> list[dict[str, Any]]:
    """Flatten every round from every file into {player, game, source_file, round,
    correct, answer_time_sec, recognized} — one record per (player, round)."""
    records: list[dict[str, Any]] = []
    for path in files:
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        schema = detect_schema(data)

        if schema == "gamelog":
            game = None
            for e in data["entries"]:
                if e["eventType"] != "round":
                    continue
                game = e.get("game", game)
                records.append(
                    {
                        "player": e.get("name") or "?",
                        "game": game,
                        "source_file": path.name,
                        "round": e.get("round"),
                        "correct": bool(e.get("correct")),
                        "answer_time_sec": r2(e.get("answerTimeSec")),
                        "recognized": bool(e.get("recognized")),
                    }
                )

        elif schema == "sessionlog":
            game = data.get("gameName")
            player_left = data.get("playerLeft")
            player_right = data.get("playerRight")
            for r in data.get("rounds", []):
                winner = r.get("winnerName") or None
                for side_player, recognized_field in (
                    (player_left, "playerLeftRecognized"),
                    (player_right, "playerRightRecognized"),
                ):
                    records.append(
                        {
                            "player": side_player or "?",
                            "game": game,
                            "source_file": path.name,
                            "round": r.get("round"),
                            "correct": winner == side_player if winner else False,
                            "answer_time_sec": r2(r.get("responseTimeSeconds")),
                            "recognized": bool(r.get(recognized_field)),
                        }
                    )
    return records


def collect_recognition_records(files: list[Path]) -> list[dict[str, Any]]:
    """Flatten every standalone recognition attempt (both GameLog and Recognition_* files)
    into {player, game, source_file, recognized, elapsed_sec}."""
    records: list[dict[str, Any]] = []
    for path in files:
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        schema = detect_schema(data)
        if schema not in ("gamelog", "recognition"):
            continue
        game = None
        for e in data["entries"]:
            if schema == "gamelog" and e.get("eventType") != "recognition":
                continue
            game = e.get("game", game)
            records.append(
                {
                    "player": e.get("name") or "?",
                    "game": game,
                    "source_file": path.name,
                    "recognized": bool(e.get("recognized")),
                    "elapsed_sec": r2(e.get("recognizeElapsedSec", e.get("elapsedSec"))),
                }
            )
    return records


# Player_1 = left slot's fallback name, Player_2 = right slot's fallback name (used whenever
# face recognition fails for that slot). The two are consistently tied to left/right, so they
# stay separate players rather than being merged.


def build_player_profiles(
    round_records: list[dict[str, Any]], recog_records: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    """One row per player name (not per game/slot/file) — this is the answer to
    "evaluate this specific person across everything they played", not a per-game view."""
    by_player: dict[str, dict[str, list]] = defaultdict(
        lambda: {"rounds": [], "recog": [], "games": set(), "sessions": set()}
    )
    for r in round_records:
        p = by_player[r["player"]]
        p["rounds"].append(r)
        if r["game"]:
            p["games"].add(r["game"])
        p["sessions"].add(r["source_file"])
    for r in recog_records:
        p = by_player[r["player"]]
        p["recog"].append(r)
        if r["game"]:
            p["games"].add(r["game"])
        p["sessions"].add(r["source_file"])

    rows = []
    for player, bucket in sorted(by_player.items()):
        rounds = bucket["rounds"]
        recog = bucket["recog"]
        correct = sum(1 for x in rounds if x["correct"])
        rows.append(
            {
                "player": player,
                "sessions_played": len(bucket["sessions"]),
                "games_played": ", ".join(sorted(bucket["games"])) or "-",
                "total_rounds": len(rounds),
                "rounds_correct": correct,
                "accuracy_pct": pct(correct, len(rounds)),
                "avg_answer_time_sec": mean([x["answer_time_sec"] for x in rounds]),
                "recognition_attempts": len(recog),
                "recognition_success_pct": pct(
                    sum(1 for x in recog if x["recognized"]), len(recog)
                ),
                "avg_recognize_elapsed_sec": mean([x["elapsed_sec"] for x in recog]),
            }
        )
    return rows


FIELDS_PLAYER = [
    "player",
    "sessions_played",
    "games_played",
    "total_rounds",
    "rounds_correct",
    "accuracy_pct",
    "avg_answer_time_sec",
    "recognition_attempts",
    "recognition_success_pct",
    "avg_recognize_elapsed_sec",
]


# ── Round-by-round detail (round 1, round 2, ... with left/right side by side) ──────────────


def rounds_gamelog(data: dict, source: str) -> list[dict[str, Any]]:
    """One row per round NUMBER, with left_* and right_* columns side by side —
    each slot in a GameLog file logs its own independent round counter/question."""
    game = None
    by_round: dict[int, dict[str, Any]] = defaultdict(dict)
    for e in data["entries"]:
        if e["eventType"] != "round":
            continue
        game = e.get("game", game)
        slot = e.get("slot", "?")
        by_round[e["round"]][slot] = e

    rows = []
    for round_num in sorted(by_round):
        sides = by_round[round_num]
        left = sides.get("left")
        right = sides.get("right")
        rows.append(
            {
                "source_file": source,
                "game": game,
                "round": round_num,
                "left_player": left["name"] if left else None,
                "left_recognized": left["recognized"] if left else None,
                "left_question": left["question"] if left else None,
                "left_answer": left["answer"] if left else None,
                "left_correct": left["correct"] if left else None,
                "left_answer_time_sec": r2(left["answerTimeSec"]) if left else None,
                "right_player": right["name"] if right else None,
                "right_recognized": right["recognized"] if right else None,
                "right_question": right["question"] if right else None,
                "right_answer": right["answer"] if right else None,
                "right_correct": right["correct"] if right else None,
                "right_answer_time_sec": r2(right["answerTimeSec"]) if right else None,
            }
        )
    return rows


def rounds_sessionlog(data: dict, source: str) -> list[dict[str, Any]]:
    """One row per round, shared question, per-side click summary + winner."""
    game = data.get("gameName")
    player_left = data.get("playerLeft")
    player_right = data.get("playerRight")
    rows = []
    for r in data.get("rounds", []):
        clicks = r.get("clicks", [])
        left_clicks = [c for c in clicks if c.get("side") == "Left"]
        right_clicks = [c for c in clicks if c.get("side") == "Right"]
        winner = r.get("winnerName") or None
        rows.append(
            {
                "source_file": source,
                "game": game,
                "round": r.get("round"),
                "topic": r.get("topic"),
                "question_type": r.get("questionType"),
                "answer_mode": r.get("answerMode"),
                "left_player": player_left,
                "left_recognized": bool(r.get("playerLeftRecognized")),
                "left_clicks": "; ".join(
                    f"{c.get('answerIndex')}:{c.get('result')}" for c in left_clicks
                ),
                "left_correct": winner == player_left if winner else False,
                "right_player": player_right,
                "right_recognized": bool(r.get("playerRightRecognized")),
                "right_clicks": "; ".join(
                    f"{c.get('answerIndex')}:{c.get('result')}" for c in right_clicks
                ),
                "right_correct": winner == player_right if winner else False,
                "response_time_sec": r2(r.get("responseTimeSeconds")),
                "winner": winner,
            }
        )
    return rows


def rounds_for_file(path: Path) -> tuple[str, list[dict[str, Any]]]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return "unknown", []
    schema = detect_schema(data)
    if schema == "gamelog":
        return schema, rounds_gamelog(data, path.name)
    if schema == "sessionlog":
        return schema, rounds_sessionlog(data, path.name)
    return schema, []  # "recognition" schema has no round data at all


FIELDS = [
    "source_file",
    "game",
    "player",
    "slot",
    "rounds_played",
    "rounds_correct",
    "accuracy_pct",
    "avg_answer_time_sec",
    "recognition_attempts",
    "recognition_success_pct",
    "avg_recognize_elapsed_sec",
]

FIELDS_ROUNDS_GAMELOG = [
    "source_file", "game", "round",
    "left_player", "left_recognized", "left_question", "left_answer", "left_correct", "left_answer_time_sec",
    "right_player", "right_recognized", "right_question", "right_answer", "right_correct", "right_answer_time_sec",
]

FIELDS_ROUNDS_SESSIONLOG = [
    "source_file", "game", "round", "topic", "question_type", "answer_mode",
    "left_player", "left_recognized", "left_clicks", "left_correct",
    "right_player", "right_recognized", "right_clicks", "right_correct",
    "response_time_sec", "winner",
]


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("log_dir", nargs="?", default="FRTest_logs")
    ap.add_argument("-o", "--out", default="Tools/player_summary.csv")
    ap.add_argument("--by-session-out", default="Tools/game_log_summary_by_session.csv")
    ap.add_argument("--rounds-out", default="Tools/game_log_rounds.csv")
    ap.add_argument(
        "--no-round-table", action="store_true",
        help="skip printing the per-file round-by-round console table",
    )
    args = ap.parse_args()

    log_dir = Path(args.log_dir)
    if not log_dir.is_dir():
        sys.exit(f"Log directory not found: {log_dir}")

    files = sorted(log_dir.glob("*.json"))
    if not files:
        sys.exit(f"No .json log files found in {log_dir}")

    by_session_rows: list[dict[str, Any]] = []
    gamelog_rounds: list[dict[str, Any]] = []
    sessionlog_rounds: list[dict[str, Any]] = []
    for f in files:
        by_session_rows.extend(parse_file(f))
        schema, rround = rounds_for_file(f)
        if schema == "gamelog":
            gamelog_rounds.extend(rround)
        elif schema == "sessionlog":
            sessionlog_rounds.extend(rround)

    if not by_session_rows:
        sys.exit("No parseable rows found.")

    # player-based aggregation (the primary output) — every round & recognition attempt
    # grouped by the resolved player NAME across every session/game, not by game/slot.
    round_records = collect_round_records(files)
    recog_records = collect_recognition_records(files)
    player_rows = build_player_profiles(round_records, recog_records)

    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=FIELDS_PLAYER)
        writer.writeheader()
        writer.writerows(player_rows)

    by_session_out = Path(args.by_session_out)
    by_session_out.parent.mkdir(parents=True, exist_ok=True)
    with by_session_out.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=FIELDS)
        writer.writeheader()
        writer.writerows(by_session_rows)

    rounds_out = Path(args.rounds_out)
    if gamelog_rounds:
        p = rounds_out.with_name(rounds_out.stem + "_gamelog" + rounds_out.suffix)
        with p.open("w", newline="", encoding="utf-8") as fh:
            writer = csv.DictWriter(fh, fieldnames=FIELDS_ROUNDS_GAMELOG)
            writer.writeheader()
            writer.writerows(gamelog_rounds)
    if sessionlog_rounds:
        p = rounds_out.with_name(rounds_out.stem + "_sessionlog" + rounds_out.suffix)
        with p.open("w", newline="", encoding="utf-8") as fh:
            writer = csv.DictWriter(fh, fieldnames=FIELDS_ROUNDS_SESSIONLOG)
            writer.writeheader()
            writer.writerows(sessionlog_rounds)

    # console: player-based summary (primary view — one row per real person)
    print(f"Parsed {len(files)} file(s) -> {len(player_rows)} player(s)\n")
    print("=" * 70)
    print("PLAYER SUMMARY (across all games/sessions)")
    print("=" * 70)
    name_w = max(10, max((len(r["player"]) for r in player_rows), default=10)) + 1
    header = (
        f"{'player':<{name_w}} {'sessions':>8} {'rounds':>7} {'acc%':>6} {'avgAns':>8} "
        f"{'recog%':>7} {'avgRecog':>9}  games"
    )
    print(header)
    print("-" * len(header))
    for r in player_rows:
        print(
            f"{r['player']:<{name_w}} {r['sessions_played']:>8} {r['total_rounds']:>7} "
            f"{str(r['accuracy_pct'] if r['accuracy_pct'] is not None else '-'):>6} "
            f"{str(r['avg_answer_time_sec'] if r['avg_answer_time_sec'] is not None else '-'):>8} "
            f"{str(r['recognition_success_pct'] if r['recognition_success_pct'] is not None else '-'):>7} "
            f"{str(r['avg_recognize_elapsed_sec'] if r['avg_recognize_elapsed_sec'] is not None else '-'):>9}  "
            f"{r['games_played']}"
        )
    print(f"\nPlayer summary CSV written to {out_path}")
    print(
        "Note: 'Player_1' (left) / 'Player_2' (right) are fallback names used whenever face "
        "recognition failed for that slot — each may mix multiple different real people who "
        "went unrecognized on that side."
    )
    print(f"Per-session (by game/slot) CSV written to {by_session_out}")

    # console: round-by-round detail, grouped per file
    if not args.no_round_table and (gamelog_rounds or sessionlog_rounds):
        print("\n" + "=" * 70)
        print("ROUND-BY-ROUND DETAIL")
        print("=" * 70)

        by_file: dict[str, list[dict]] = defaultdict(list)
        for r in gamelog_rounds:
            by_file[r["source_file"]].append(r)
        for source, rlist in by_file.items():
            print(f"\n[{source}]")
            rhdr = f"  {'round':>5} | {'LEFT':<28} | {'RIGHT':<28}"
            print(rhdr)
            print("  " + "-" * (len(rhdr) - 2))
            for r in rlist:
                l = f"{r['left_player'] or '-'}: {r['left_question'] or ''} -> {r['left_answer'] or ''} " \
                    f"({'OK' if r['left_correct'] else 'X'}, {r['left_answer_time_sec']}s)" if r["left_player"] else "-"
                rr = f"{r['right_player'] or '-'}: {r['right_question'] or ''} -> {r['right_answer'] or ''} " \
                     f"({'OK' if r['right_correct'] else 'X'}, {r['right_answer_time_sec']}s)" if r["right_player"] else "-"
                print(f"  {r['round']:>5} | {l:<28} | {rr:<28}")

        by_file2: dict[str, list[dict]] = defaultdict(list)
        for r in sessionlog_rounds:
            by_file2[r["source_file"]].append(r)
        for source, rlist in by_file2.items():
            print(f"\n[{source}]")
            rhdr = f"  {'round':>5} | {'LEFT':<30} | {'RIGHT':<30} | {'winner':<10}"
            print(rhdr)
            print("  " + "-" * (len(rhdr) - 2))
            for r in rlist:
                l = f"{r['left_player']}: {r['left_clicks']} ({'OK' if r['left_correct'] else 'X'})"
                rr = f"{r['right_player']}: {r['right_clicks']} ({'OK' if r['right_correct'] else 'X'})"
                print(f"  {r['round']:>5} | {l:<30} | {rr:<30} | {r['winner'] or '-':<10}")

    if gamelog_rounds:
        print(f"\nRound-detail CSV (GameLog) written to {rounds_out.with_name(rounds_out.stem + '_gamelog' + rounds_out.suffix)}")
    if sessionlog_rounds:
        print(f"Round-detail CSV (SessionLog) written to {rounds_out.with_name(rounds_out.stem + '_sessionlog' + rounds_out.suffix)}")


if __name__ == "__main__":
    main()
