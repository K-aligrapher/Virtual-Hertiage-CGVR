"""Summarises the CSV files exported by the in-app student study.

    python Tools/analyze_study.py study_data            # folder holding the heritage_study_*.csv files
    python Tools/analyze_study.py study_data --out report.md --all-csv study_all.csv

Reports learning gain (pre/post knowledge test) and comfort (SSQ before/after, FMS during, 7-point ratings),
overall and per condition (platform, audio guide). Uses only the standard library; if SciPy is installed it is
used for the exact tests, otherwise a built-in Student t implementation gives the p-values.
"""
import argparse
import csv
import glob
import math
import os
import statistics as st


def num(row, key):
    try:
        return float(row[key])
    except (KeyError, ValueError, TypeError):
        return float("nan")


def clean(values):
    return [v for v in values if not math.isnan(v)]


def mean_sd(values):
    values = clean(values)
    if not values:
        return "n/a"
    sd = st.stdev(values) if len(values) > 1 else 0.0
    return f"{st.mean(values):.2f} +/- {sd:.2f}"


# ---- Student t distribution (survival function) without SciPy ----
def _betacf(a, b, x):
    tiny, qab, qap, qam = 1e-30, a + b, a + 1, a - 1
    c, d = 1.0, 1.0 - qab * x / qap
    d = 1.0 / (d if abs(d) > tiny else tiny)
    h = d
    for m in range(1, 300):
        m2 = 2 * m
        aa = m * (b - m) * x / ((qam + m2) * (a + m2))
        d = 1.0 + aa * d; d = 1.0 / (d if abs(d) > tiny else tiny)
        c = 1.0 + aa / c; c = c if abs(c) > tiny else tiny
        h *= d * c
        aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
        d = 1.0 + aa * d; d = 1.0 / (d if abs(d) > tiny else tiny)
        c = 1.0 + aa / c; c = c if abs(c) > tiny else tiny
        delta = d * c
        h *= delta
        if abs(delta - 1.0) < 3e-12:
            break
    return h


def _betai(a, b, x):
    if x <= 0:
        return 0.0
    if x >= 1:
        return 1.0
    bt = math.exp(math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b) + a * math.log(x) + b * math.log(1 - x))
    if x < (a + 1) / (a + b + 2):
        return bt * _betacf(a, b, x) / a
    return 1 - bt * _betacf(b, a, 1 - x) / b


def t_two_sided_p(t, df):
    return _betai(df / 2.0, 0.5, df / (df + t * t))


def paired(a, b):
    """Paired t-test on (b - a). Returns (n, mean difference, t, p, Cohen's dz)."""
    diffs = [y - x for x, y in zip(a, b) if not (math.isnan(x) or math.isnan(y))]
    n = len(diffs)
    if n < 2:
        return n, float("nan"), float("nan"), float("nan"), float("nan")
    m, sd = st.mean(diffs), st.stdev(diffs)
    if sd == 0:
        return n, m, float("inf") if m else 0.0, 0.0 if m else 1.0, float("inf") if m else 0.0
    t = m / (sd / math.sqrt(n))
    return n, m, t, t_two_sided_p(t, n - 1), m / sd


def welch(a, b):
    a, b = clean(a), clean(b)
    if len(a) < 2 or len(b) < 2:
        return float("nan"), float("nan"), float("nan")
    va, vb = st.variance(a) / len(a), st.variance(b) / len(b)
    se = math.sqrt(va + vb)
    if se == 0:
        return 0.0, float("nan"), float("nan")
    t = (st.mean(b) - st.mean(a)) / se
    df = (va + vb) ** 2 / (va ** 2 / (len(a) - 1) + vb ** 2 / (len(b) - 1))
    pooled = math.sqrt(((len(a) - 1) * st.variance(a) + (len(b) - 1) * st.variance(b)) / (len(a) + len(b) - 2))
    return t, t_two_sided_p(t, df), (st.mean(b) - st.mean(a)) / pooled if pooled else float("nan")


def ssq_band(total):
    if math.isnan(total):
        return ""
    for limit, label in ((5, "negligible"), (10, "minimal"), (15, "significant"), (20, "concerning")):
        if total < limit:
            return label
    return "problematic"


def fmt_p(p):
    return "n/a" if math.isnan(p) else ("<0.001" if p < 0.001 else f"{p:.3f}")


def load(folder, include_withdrawn):
    rows = []
    for path in sorted(glob.glob(os.path.join(folder, "**", "heritage_study_*.csv"), recursive=True)):
        with open(path, encoding="utf-8-sig", newline="") as f:
            for row in csv.DictReader(f):
                row["_file"] = os.path.basename(path)
                if include_withdrawn or row.get("completed", "yes") == "yes":
                    rows.append(row)
    return rows


def condition(row):
    guide = "guide " + row.get("guide_language", "") if row.get("guide") == "on" else "no guide"
    return f"{row.get('platform', '?')} / {guide}"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folder")
    ap.add_argument("--out", help="write the Markdown report here")
    ap.add_argument("--all-csv", help="write all rows combined into one CSV")
    ap.add_argument("--include-withdrawn", action="store_true")
    args = ap.parse_args()

    rows = load(args.folder, args.include_withdrawn)
    if not rows:
        raise SystemExit("No heritage_study_*.csv files with completed sessions found in " + args.folder)
    out = []
    P = out.append

    P(f"# Student study report\n\nParticipants analysed: **{len(rows)}**"
      f"{' (including withdrawn)' if args.include_withdrawn else ' (completed sessions only)'}\n")

    pre, post = [num(r, "pre_percent") for r in rows], [num(r, "post_percent") for r in rows]
    n, m, t, p, dz = paired(pre, post)
    P("## Learning gain\n")
    P(f"- Knowledge score before: **{mean_sd(pre)}** %, after: **{mean_sd(post)}** %")
    P(f"- Mean improvement: **{m:.1f} percentage points** (paired t({n - 1}) = {t:.2f}, p = {fmt_p(p)}, Cohen's dz = {dz:.2f})")
    P(f"- Normalised gain (Hake) g = (post - pre) / (100 - pre): **{mean_sd([num(r, 'normalized_gain') for r in rows])}**  "
      "(g < 0.3 low, 0.3-0.7 medium, > 0.7 high)\n")

    s0, s1 = [num(r, "ssq_total_pre") for r in rows], [num(r, "ssq_total_post") for r in rows]
    n, m, t, p, dz = paired(s0, s1)
    P("## Comfort\n")
    P(f"- SSQ total score before: **{mean_sd(s0)}**, after: **{mean_sd(s1)}**; mean change **{m:+.1f}** "
      f"(paired t({n - 1}) = {t:.2f}, p = {fmt_p(p)}, dz = {dz:.2f})")
    post_mean = st.mean(clean(s1)) if clean(s1) else float("nan")
    P(f"- Mean post-exposure SSQ total is in the '{ssq_band(post_mean)}' band (Kennedy 1993 / Stanney 1997 bands: <5, 5-10, 10-15, 15-20, >20)")
    for key, label in (("ssq_nausea", "Nausea"), ("ssq_oculomotor", "Oculomotor"), ("ssq_disorientation", "Disorientation")):
        P(f"- {label}: before {mean_sd([num(r, key + '_pre') for r in rows])}, after {mean_sd([num(r, key + '_post') for r in rows])}")
    P(f"- Highest in-session sickness rating (FMS, 0-20): {mean_sd([num(r, 'fms_max') for r in rows])}; "
      f"{sum(1 for r in rows if num(r, 'fms_max') >= 15)} participant(s) reported 15 or more")
    ratings = [k for k in rows[0] if k.startswith("rating_")]
    for k in ratings:
        P(f"- {k.replace('rating_', '').capitalize()} (1-7): {mean_sd([num(r, k) for r in rows])}")
    P("")

    groups = {}
    for r in rows:
        groups.setdefault(condition(r), []).append(r)
    P("## By condition\n")
    P("| Condition | n | Pre % | Post % | Normalised gain | SSQ change | FMS max | Comfort (1-7) | Presence (1-7) |")
    P("|---|---|---|---|---|---|---|---|---|")
    for name, g in sorted(groups.items()):
        change = [b - a for a, b in zip([num(r, "ssq_total_pre") for r in g], [num(r, "ssq_total_post") for r in g])]
        P(f"| {name} | {len(g)} | {mean_sd([num(r, 'pre_percent') for r in g])} | {mean_sd([num(r, 'post_percent') for r in g])} | "
          f"{mean_sd([num(r, 'normalized_gain') for r in g])} | {mean_sd(change)} | {mean_sd([num(r, 'fms_max') for r in g])} | "
          f"{mean_sd([num(r, 'rating_comfort') for r in g])} | {mean_sd([num(r, 'rating_presence') for r in g])} |")
    P("")

    if len(groups) == 2:
        (na, ga), (nb, gb) = sorted(groups.items())
        t, p, d = welch([num(r, "normalized_gain") for r in ga], [num(r, "normalized_gain") for r in gb])
        P(f"Normalised gain, **{nb}** minus **{na}**: Welch t = {t:.2f}, p = {fmt_p(p)}, Cohen's d = {d:.2f}\n")
    elif len(groups) > 2:
        P("More than two conditions: compare them with an ANOVA or Kruskal-Wallis in your statistics package, using the combined CSV (--all-csv).\n")

    P("## Notes\n")
    P("- With small samples (n < 30 per group) prefer reporting effect sizes and confidence intervals; check the paired differences are roughly symmetric or use a Wilcoxon signed-rank test.")
    P("- Pre- and post-tests use the same 12 questions in a different random order. Say so in the write-up; if retest effects worry you, use two parallel forms.")
    P("- SSQ pre/post compares symptoms before and after the session; a baseline (pre) above 0 is normal.")

    text = "\n".join(out)
    print(text)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(text + "\n")
    if args.all_csv:
        keys = []
        for r in rows:
            for k in r:
                if k not in keys:
                    keys.append(k)
        with open(args.all_csv, "w", encoding="utf-8-sig", newline="") as f:
            w = csv.DictWriter(f, fieldnames=keys)
            w.writeheader()
            w.writerows(rows)


if __name__ == "__main__":
    main()
