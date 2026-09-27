# Jev Connector for OutSystems ODC

An [OutSystems Developer Cloud](https://www.outsystems.com/odc) external library that calls
[Jev](https://docs.typesafe.ai), TypeSafe AI's "System One" evaluation model, from any ODC app or
library — no HTTP/JSON plumbing required.

Jev is not a chat model: it evaluates a **state** (text, a chat log, a ticket, any JSON) against
one or more **typed questions** and returns calibrated probabilities instead of prose. That makes
it a good fit for the decisions that usually sit *around* an LLM call in an agent or automation:
routing, triage, guardrails, sentiment/urgency scoring, deduplication checks — the kind of thing
this connector's actions map directly onto.

> Unofficial, community-built connector. Not affiliated with or endorsed by TypeSafe AI. Jev was
> in early access behind a waitlist as of September 2026 — get an API key at
> [typesafe.ai](https://typesafe.ai) before using this connector.

## What it gives you

Four Server Actions, exposed via `IJevClient`:

| Action | Maps to Jev's... | Use it for |
|---|---|---|
| `EvaluateNoul` | `noul` question | A single yes/no probability (e.g. "Is this urgent?", "Is this a duplicate lead?") |
| `EvaluateChoice` | `choice` question | Picking one of up to 255 labeled options (e.g. routing a ticket to a team) |
| `EvaluateScore` | `score` question | Rating along an ordered 2–10 level rubric (e.g. frustration level) |
| `EvaluateRaw` | the full `questions` object | Advanced: several questions in one call, structured/JSON state, nested instructions — anything the typed actions don't cover |

Every action:
- Takes the state, the question, and your API key as plain OutSystems input parameters — no
  Structure gymnastics for the common cases (`EvaluateChoice`'s options and `EvaluateScore`'s
  levels are simple Lists).
- Retries automatically on `429` (rate limited) and `529` (overloaded) with exponential backoff,
  honoring the API's `Retry-After` header, per [TypeSafe's own guidance](https://docs.typesafe.ai/api#handling-rate-limits).
- **Never raises an exception.** Failures come back as data — `Success`, `ErrorCode`,
  `ErrorMessage`, `StatusCode` on the result structure — so your flow can branch on them instead
  of routing everything through an Exception Handler. See [Error handling](#error-handling). The
  API key is never logged or echoed in an error message.
- Lets you override `apiBaseUrl`, so you can point it at a gateway/proxy (LiteLLM, Opper, orq.ai)
  instead of `api.typesafe.ai` directly if that's how your org routes model traffic.

## Setup

1. Get a TypeSafe API key (waitlist at [typesafe.ai](https://typesafe.ai) as of Sep 2026).
2. Build and upload the library (see [Build & publish](#build--publish-to-odc) below).
3. In your ODC app, add the `JevClient` library dependency and call its actions.
4. **Store the API key as an ODC app Configuration Value** (or your own secrets pattern), not as
   a hardcoded literal in a flow — every action takes it as a plain parameter, so wire it in from
   wherever your app already keeps secrets.

## Example: triage an inbound message in three calls

```
Team          = EvaluateChoice(ApiKey, MessageBody, "Which team should handle this?",
                    [ {Option:"billing", Description:"Payments, invoicing, refunds"},
                      {Option:"technical", Description:"Bugs, outages, integrations"},
                      {Option:"sales", Description:"Pricing, upgrades, new accounts"} ])

Urgency       = EvaluateNoul(ApiKey, MessageBody, "Does this convey urgency?")

Frustration   = EvaluateScore(ApiKey, MessageBody, "How frustrated is the customer?",
                    ["Calm", "Frustrated", "Very angry"])

If not Urgency.Success or not Frustration.Success or not Team.Success Then
    RouteToDefaultQueue()          // degrade gracefully; the reason is in .ErrorMessage
Else If Urgency.Noul > 0.7 or Frustration.Score > 1.5 Then
    EscalateToHuman()
Else
    RouteTo(Team.Choice)
End If
```

Note the `Success` check up front. On a failed call `Noul` and `Score` come back as `0`, which
reads as a confident "no" / lowest level — so a flow that skips the check silently routes on a
wrong answer instead of failing loudly. Check it first, every time.

Three independent judgments, no prompt engineering, and — per TypeSafe's published pricing at
launch — output tokens are free and a call typically lands in well under a second.

## Action reference

Every result structure starts with the same four fields — `Success`, `ErrorCode`, `ErrorMessage`,
`StatusCode` — described under [Error handling](#error-handling). The per-action fields below are
only meaningful when `Success` is `True`.

### `EvaluateNoul`

| Parameter | Type | Notes |
|---|---|---|
| `ApiKey` | Text | Required |
| `State` | Text | The content to evaluate |
| `Instructions` | Text | The yes/no question |
| `CriteriaTrue` | Text | Optional: what counts as "yes" |
| `CriteriaFalse` | Text | Optional: what counts as "no" |
| `Model` | Text | Default `"jev-latest"` |
| `ApiBaseUrl` | Text | Default `"https://api.typesafe.ai"` |

Returns `JevNoulResult { Success, ErrorCode, ErrorMessage, StatusCode, Model, Noul, InputTokens,
OutputTokens }`. `Noul` is a probability from 0 to 1 — apply your own threshold for a boolean
decision, same as TypeSafe recommends.

### `EvaluateChoice`

`Options` is a `List<JevChoiceCriterion>` (`{ Option, Description }`), 1–255 entries. Returns
`JevChoiceResult { Success, ErrorCode, ErrorMessage, StatusCode, Model, Choice, Confidence,
Probabilities, InputTokens, OutputTokens }`, where `Probabilities` is a
`List<JevChoiceProbability>` covering every option (empty on failure).

### `EvaluateScore`

`Levels` is an ordered `List<Text>` (low to high), 2–10 entries. Returns
`JevScoreResult { Success, ErrorCode, ErrorMessage, StatusCode, Model, Score, Confidence, Levels,
InputTokens, OutputTokens }`, where `Levels` is a `List<JevScoreLevelProbability>`
(`{ Level, Description, Probability }`, empty on failure) and `Score` is the probability-weighted
value across your rubric (can land between two levels).

### `EvaluateRaw`

For anything the three actions above don't cover: multiple questions in a single round trip,
structured `state` (pass a JSON object/array as text — it's detected automatically and sent as
structured data rather than a quoted string), or nested `instructions`/`criteria`. `QuestionsJson`
is the `questions` object exactly as documented at
[docs.typesafe.ai/api](https://docs.typesafe.ai/api). Returns
`JevRawResult { Success, ErrorCode, ErrorMessage, StatusCode, Model, AnswersJson, InputTokens,
OutputTokens }` — parse `AnswersJson` with `JSONDeserialize` or OutSystems' JSON widget. On
failure `AnswersJson` is empty, so check `Success` before parsing it.

## Error handling

**No action raises an exception.** Every one returns normally, and the result structure tells you
what happened through four fields:

| Field | Type | Meaning |
|---|---|---|
| `Success` | Boolean | `True` when the call worked. **Check this first, always.** |
| `ErrorCode` | Text | Stable token to branch on. Empty when `Success` is `True`. |
| `ErrorMessage` | Text | Human-readable reason, including TypeSafe's own error detail. Empty on success. Never contains your API key. |
| `StatusCode` | Integer | HTTP status from the API, or `0` when the call never reached it. |

This is deliberate: an ODC Exception Handler can only catch a *generic* error from an external
library, which leaves no way to tell a bad API key from a rate limit. Returning the failure as
data gives your flow something to branch on. Failures are still written to the ODC Portal logs.

> ⚠️ **On failure the payload fields are zeroed, not absent.** `Noul` and `Score` come back as
> `0`, `Choice` and `AnswersJson` as empty text, `Probabilities` and `Levels` as empty lists. A
> `0` probability reads as a confident "no" — so a flow that forgets the `Success` check doesn't
> just miss the error, it acts on a wrong answer. This is the one sharp edge of the pattern.

### `ErrorCode` values

| `ErrorCode` | `StatusCode` | Cause | Retried internally? | What to do |
|---|---|---|---|---|
| `Validation` | `0` | A parameter was rejected before any HTTP call: empty `ApiKey`/`Instructions`, 0 or >255 options, <2 or >10 levels, malformed `QuestionsJson`. | No — never sent | Fix the inputs. Retrying is pointless; this is a bug in the calling flow. |
| `Unauthorized` | `401` | Missing or invalid API key. | No | Check the Configuration Value holding the key. Don't retry. |
| `InvalidRequest` | `422` | The API rejected the request body — usually a criteria/rubric shape it won't accept, or a state over the context limit. | No | Inspect `ErrorMessage` for TypeSafe's detail and fix the question. Don't retry. |
| `RateLimit` | `429` | Rate limited, and the 4 internal attempts were all rate limited. | Yes, with backoff | Queue the work and retry later, or throttle upstream. |
| `Overloaded` | `529` | TypeSafe is overloaded, and all 4 internal attempts failed. | Yes, with backoff | Retry later; nothing is wrong with your request. |
| `Network` | `0` | The API was unreachable or timed out on all 4 attempts (DNS, TLS, proxy, 30s timeout). | Yes, with backoff | Check outbound connectivity and `ApiBaseUrl`. Safe to retry later. |
| `BadResponse` | `200` or `0` | The API answered successfully but the body was empty, unparseable, or missing the fields this connector expects. | No | Usually an API version drift — check `ErrorMessage` and whether the connector needs updating. |
| `HttpError` | any other | Any non-success status not listed above (e.g. `500`, `503`). | No | Inspect `StatusCode` and `ErrorMessage`. |
| `Unexpected` | `0` | An unforeseen failure inside the connector itself. | No | Shouldn't happen; `ErrorMessage` carries the detail and the ODC log has the stack trace. |

These tokens are part of the connector's contract — flows branch on them, so they don't change
without a major version bump. A reasonable default is to branch on `RateLimit`/`Overloaded`/
`Network` (transient — back off and retry) versus everything else (not transient — log and
degrade).

## Limitations (as of Sep 2026, early access)

- Jev 1.13 has a 64k total context limit, 32k for the state plus the longest single question.
- `EvaluateChoice` accepts at most 255 options; `EvaluateScore` requires 2–10 levels.
- No streaming — Jev's contract is synchronous request/response by design.
- Early access is waitlist-gated; expect the API surface (`jev-latest` vs pinned versions like
  `jev-1.13.0`) to evolve — this connector always requests `jev-latest` by default so you don't
  have to chase version bumps, but you can pin a version via the `Model` parameter.

## Build & publish to ODC

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and normal internet
access (to restore `OutSystems.ExternalLibraries.SDK` and the test packages from nuget.org).

```bash
./scripts/build-and-package.sh            # cross-platform publish
# or
./scripts/build-and-package.sh linux-x64  # if you hit runtime-specific dependency issues
```

This runs the unit test suite, publishes the library in Release mode, and zips the output to
`ExternalLibrary.zip`. Upload that file in the ODC Portal under **Extend your apps with custom
code → Upload external logic**, then create, publish and release the external library as usual.
See [OutSystems' own guide](https://success.outsystems.com/documentation/outsystems_developer_cloud/building_apps/extend_your_apps_with_custom_code/)
for that part of the flow.

## Project layout

```
src/OutSystems.JevConnector/     the external library (IJevClient, JevClient, Structures/)
tests/OutSystems.JevConnector.Tests/   xUnit tests (fake HTTP handler, no live API calls)
scripts/build-and-package.sh     restore, test, publish, zip
dev/offline-shim-smoke-tests/    dev-only: how this was verified without nuget.org access
                                  (not needed to build or use the connector — see its own README)
```

## License

MIT. See `LICENSE`.
