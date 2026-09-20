# P10-02 economy probability and evidence policy

Policy `p10-02-economy-v1` is a local harness gate, not a gameplay change or an
overall capacity verdict. It covers ordinary starter gathering and the twenty
shipped apprentice Cooking work orders. Other economic randomness is outside
this policy. The existing exact inventory, kinah, quest and persistence checks
remain active throughout every attempt.

## Source-derived completion probabilities

Reference: `../aion-server` at `ce54b7931`, `AbstractCraftTask`, `GatheringTask`,
`CraftingTask`, `CraftConfig` and `commons/utils/Rnd`. No Java server or runtime
comparison is used. Production defaults have a 33% failure chance **per progress
step** at skill lead zero; completing an activity is a race between two bars,
not a single step roll.

`SoakProgressProbability` derives the discrete distribution of each positive
bar increment, including blue/purple progress, skill bonuses, Java rounding and
craft's intermediate integer truncation. It integrates over the uniform 24-bit
float inputs by finding integer-output boundaries, without sampling a success
percentage from the server. The float scaling/upper-endpoint rules are pinned
to [OpenJDK 25 RandomGenerator](https://github.com/openjdk/jdk/blob/jdk-25-ga/src/java.base/share/classes/java/util/random/RandomGenerator.java)
and [RandomSupport](https://github.com/openjdk/jdk/blob/jdk-25-ga/src/java.base/share/classes/jdk/internal/util/random/RandomSupport.java).

One-dimensional convolution gives the number of success updates `S` and failure
updates `F` needed to fill their respective 1,000-point bars. Independently of
those increment sizes, each update chooses success with probability `p`.
For fixed required update counts, the win probability obeys:

```
R(0, f) = 1; R(s, 0) = 0
R(s, f) = p R(s-1, f) + (1-p) R(s, f-1)
completion = sum over s,f of P(S=s) P(F=f) R(s,f)
```

The current lead-zero predictions are 0.7342764720618745 for gathering and
0.7945626511980944 for ordinary unlimited/common-quality crafting. These are
derived model outputs, not thresholds guessed from the earlier short LIVE runs.
Each attempt uses its own **pre-action** observed skill lead. Lead below zero is
guaranteed failure; lead at least 41 is guaranteed success. The Cooking catalog
rejects missing/non-common products, limited production, morphing, combo recipes
and recipe/skill drift instead of applying an inappropriate simple model.

Tests include closed-form fixed-bar races, conservation of probability mass,
an exhaustive 16,777,216-input check of an increment distribution, all 41
nontrivial skill leads, and ten independent, deterministic, directly interleaved
bar simulations of 150,000 attempts each. The simulations cross-check the
derivation; their sampled rates are not the LIVE null hypothesis.

## Statistical evidence

Enrollment is fixed from the population/selected activities before setup.
Every enrolled subject must supply at least twenty valid attempts, and each
activity kind must expose at least ten model-expected failures, before it can
receive a passing statistical result. Missing exposure is **insufficient**, not
success. Probability-zero/one contradictions fail immediately in the report,
even after the initial sample.

There are four predeclared tests: Gather and Craft, each tested with:

- A fixed prefix: exactly the first twenty attempts from every enrolled subject.
  Incomplete prefixes cannot receive a prefix verdict. Later attempts cannot
  replace inconvenient early results.
- A whole-stream test: every valid completed attempt, including those after the
  prefix. Each subject has its own sequential likelihood score; their arithmetic
  mean is used, not the product of independently/adaptively stopped streams.
  Thus a late biased stream is not hidden by the bounded prefix.

Each test mixes seven predeclared alternative odds multipliers:
`0.25, 0.5, 0.75, 1.25, 1.5, 2, 4`. For null probability `p`, an alternative
uses `q = odds*p/(1-p+odds*p)`. A success multiplies its evidence by `q/p`, and a
failure by `(1-q)/(1-p)`. Computation stays in log space. The reported quantity
is log evidence, **not a p-value**. The test-martingale basis for sequential
evidence and optional stopping is described by
[Shafer, Shen, Vereshchagin and Vovk](https://arxiv.org/abs/0912.4269).

Policy v1 allocates family alpha 0.001 equally across all four tests, including
partial diagnostic selections: each rejects at log evidence at least
`log(4000)`. The claim is conditional on independent uniform progress draws and
the declared task/rate model; it is not a certification of the PRNG or a proof
that every smaller regression will be detected. Insufficient exposure and a
non-rejection must not be relabelled proof of exact behavior.

The null uses a conservative probability envelope of ±0.000002. For each
alternative direction, the denominator uses the least-favourable endpoint of
that interval, retaining expectation at most one throughout the envelope.
This covers the known float endpoint gap (§7 #82): current C# bounded scaling
can return 2 for one of the 24-bit inputs to `[1,2)`, whereas Java corrects it
downward. At most 29 progress updates can occur before either bar fills, giving
an endpoint-event union bound below 0.000002 per completion. This explicitly
documented tiny numerical envelope does **not** fix the production divergence
or allowlist any logged problem. The separate parity issue remains open.

## Runtime and limitations

Every outcome trace records the precomputed expected probability and model id.
`soak-economy.json` retains enrollment, bounded prefix counters, all-attempt
counts, exposure and both tests' log evidence. Runtime rejection fails the run;
short diagnostics may finish with an explicit insufficient statistics result.
The report always sets `OverallSoakAccepted: false`: full population/workload,
two-hour telemetry, persistence, error policy and other Phase 10 gates remain
separate. No sample is rerolled, no completed quest is reset, and no production
rates/events are disabled.

Memory is bounded by the enrolled subjects and seven score accumulators per
subject/kind, regardless of attempt count. Tests cover early/late forced outcome
mutations, omitted subjects, adaptive skill expectations, fixed-prefix retention,
guaranteed outcomes, invalid input and concurrent subjects. Full LIVE statistical
acceptance still requires a completed sufficiently sampled run, not these tests.
