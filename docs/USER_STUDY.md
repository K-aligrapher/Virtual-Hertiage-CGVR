# Student study: learning gain and comfort

The app has a built-in study mode that runs the whole session on the device (desktop, browser, phone or Quest), records the answers and exports one CSV per participant. `Tools/analyze_study.py` turns those files into a report.

## What is measured

| Construct | Instrument | When | Output |
|---|---|---|---|
| **Learning gain** | 12-question multiple-choice knowledge test (with an "I do not know" option) | before and after exploring | pre %, post %, gain, Hake normalised gain `g = (post - pre) / (100 - pre)` |
| **Simulator sickness / comfort** | Simulator Sickness Questionnaire (SSQ, Kennedy et al. 1993), 16 symptoms, 0-3 | before and after | Nausea, Oculomotor, Disorientation, Total (standard weights 9.54, 7.58, 13.92, 3.74) |
| **In-session comfort** | Fast Motion Sickness score (FMS, Keshavarz and Hecht 2011), 0-20 | every 2.5 minutes during exploration | per-probe scores, maximum |
| **Experience** | 7-point agreement ratings: comfort, presence, ease of use, models helped understanding, audio guide helped (guide group only), would use again | after | one value each |
| **Behaviour** | time per temple, distance walked, teleports, number of guide narrations | automatic | columns in the CSV |
| **Background** | VR experience (1-5), prior knowledge (1-5), platform, guide on/off and language | before | columns in the CSV |

The knowledge questions are about what the app and the audio guide teach (founding date and king, jagati, garbhagriha, soapstone, twin shrines, lathe-turned columns, UNESCO dates, Vijayanagara, gopuram, mandapa). They are in `Assets/Heritage/Resources/study.json` and you can edit them. Each test shuffles the question and answer order per participant, and the pre- and post-test use different shuffles.

## Suggested design

* **One group, pre/post:** everyone does everything. Gives learning gain and comfort. Simple, but cannot separate the app from just retaking the quiz.
* **Two conditions (recommended):** randomly assign students to **with audio guide** and **without audio guide** (a between-subjects comparison of learning gain), or to **desktop** versus **headset**. The app asks the supervisor-chosen condition at the start, so it is logged. A within-subjects comparison of platforms (same student, two sessions, counterbalanced order) needs a second set of questions to avoid memorising answers.
* **Sample:** 20 to 30 students per condition is a reasonable classroom target. Report effect sizes and confidence intervals, not only p-values.

Get approval from your department or ethics committee before running it, and agree the age and health exclusions (for example, people prone to motion sickness, epilepsy, or who are unwell should not use a headset).

## Procedure (about 20 minutes per student)

1. Give each student a 3-digit participant code from a list you keep (do not record names next to the codes in the data files).
2. Start the app and open the **Student study** button in the gallery panel (the first screen). Read the consent text with the student.
3. The student enters the code, the supervisor picks the audio-guide condition (and language), then the student answers two background questions.
4. 16 comfort questions, then the 12-question pre-test.
5. **Free exploration:** the student explores the three temples. A timer shows in the panel. **Finish** unlocks after 7 minutes and the session ends by itself after 15 minutes. A comfort question (0-20) pops up every 2.5 minutes. **Stop study** is always available; stopping saves what exists and marks the row as withdrawn.
6. 16 comfort questions again, the 12-question post-test and the experience ratings.
7. The summary screen shows the score and where the file was saved.

The timings are constants at the top of `HeritageStudy.cs` (`MinSeconds`, `MaxSeconds`, `probeEvery`). Starting the Windows app with `--study-fast` shortens them to test the flow.

On headset: let the student sit on a chair, stop immediately if they feel unwell (they take the headset off, you tap **Stop study**).

## Getting the data

Every finished (or stopped) session writes `heritage_study_P<code>_<timestamp>.csv`:

| Platform | Where the file goes |
|---|---|
| Browser (desktop or phone) | downloaded by the browser when the student presses the last button |
| Windows app | `%USERPROFILE%\AppData\LocalLow\Heritage Studio\Virtual Heritage Karnataka\study\` |
| Quest (standalone) | `/sdcard/Android/data/com.heritagestudio.virtualheritagekarnataka/files/study/`, copy it out with Meta Quest Developer Hub's file manager, SideQuest, or `adb pull` |

Collect all CSV files into one folder, then:

```powershell
python Tools\analyze_study.py study_data --out report.md --all-csv study_all.csv
```

The report gives the overall pre/post comparison (paired t-test and effect size), SSQ before and after with the standard severity bands, FMS maxima, the experience ratings, a table per condition, and a Welch comparison of normalised gain when there are exactly two conditions. `study_all.csv` is every row in one file for SPSS, R or Excel.

## CSV columns (one row per participant)

`completed`, `exit_phase`, `participant`, `started_utc`, `platform`, `guide`, `guide_language`, `app_version`, `vr_experience_1to5`, `prior_knowledge_1to5`, `explore_seconds`, `seconds_gallery/belur/halebidu/hampi`, `temples_visited`, `walked_metres`, `teleports`, `guide_plays`, `pre_correct`, `post_correct`, `questions`, `pre_percent`, `post_percent`, `gain_points`, `normalized_gain`, `ssq_{nausea,oculomotor,disorientation,total}_{pre,post}`, `fms_probes_seconds_score` (`seconds:score;...`), `fms_max`, `rating_*`, then `pre_qXX`/`post_qXX` (`correct|chosen option index|seconds`, chosen = -1 for "I do not know") and `ssq_pre_1..16`/`ssq_post_1..16`.

## Limits to state in the write-up

* The pre- and post-test use the same questions. A parallel second form removes the retest effect.
* The questions follow the app's own content, so they measure learning from this app, not general architectural knowledge.
* The Hindi and Kannada audio is machine-generated neural speech from text that should be reviewed by a native speaker before it is used in a study. The on-screen captions are English only.
* SSQ symptoms such as burping or sweating are rare in short sessions; low totals are expected. FMS probes catch short-lived discomfort that the before/after SSQ can miss.
* The app records no names, images or audio of students, only the code and answers.
