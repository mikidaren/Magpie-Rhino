# Architectural workflow and streaming panel

## User requirements

The user wants a calm, approachable interface, live output and visible execution.
Architectural tasks should establish reference drawings and scale, then develop
the model in stages. An assistant ending its response must not independently
declare that a partially developed building meets the brief.

Keep this work local until the user requests publication.

## Findings

The current gateway explicitly disables streaming. The agent loop returns when
a response has no tool calls, with no remaining-task check. Its system instructions
do not establish architectural stages or explain the Rhino C# script wrapper.

The supplied failed script used the C# keyword `base` as an identifier, treated
LayerTable.FindName as an integer index and returned an object from Rhino's
void script entry point. Compilation errors are completed tool failures and can
be repaired without guessing whether geometry executed.

Reer's public `reer-ide/rhino_mcp` repository has an MIT license. Its
`rhino_mcp_remote` repository declares proprietary terms. Research informs the
workflow; this implementation uses our existing Rhino MCP connection and own UI.

The installed RhinoAI runner still selects between three internal adapters.
The compatibility solution hosts our panel in the native AI panel's public Eto
content tree and adds a selector. Native mode restores the original WebView and
agent selector; Magpie mode executes our own agent. This is a live UI adapter,
not a formal fourth backend registration. Keep one host per native panel/document,
defer selection until the opening command returns, and restore/dispose original
controls according to their owning panel's lifetime.

## Implementation choices

Use an embedded Eto WebView with our own local HTML, CSS and JavaScript.
Use system typography, clear light and dark surfaces, compact controls, separate
assistant messages and collapsible tool cards. Show the actual model output,
stage summaries and execution results. Keep private reasoning separate from the
public progress display.

Stream chat-completion SSE text and assemble tool calls by index. Execute tools
only after a complete, validated model response. Preserve non-streaming JSON
compatibility and cancellation.

Architectural mode supplies document units and model extents. It requires an
explicit task plan, drawing/scale decisions and progressive verification.
The model can update a plan only with named steps and acceptance criteria.
Completion requires fresh readback after mutations. Pending steps trigger
continued work within the call budget; unfinished work is shown as paused.
Final architectural output remains pending user review.

Track each native operation with a stable ID. Failed modifications remain in a
persistent ledger until the agent explicitly links each failure to a successful
correction of the same operation family and active plan stage, plus fresh
readback and a verification note. Unrelated successful material or view operations
cannot clear a geometry failure. Resolve failures in both architectural and direct
modes; incomplete recovery pauses honestly. This checks execution provenance,
not the semantic correctness of the architectural design.

Allow user-selected reference images and CAD/model files. Image references can
be sent to the model. Importing an image as a scaled Rhino picture frame requires
an explicit real-world width. CAD import uses Rhino's own import flow with the
user able to resolve format/unit dialogs.

## Panel bridge

The default agent geometry route uses exact profile curves, NURBS-compatible
surfaces and native Extrusion/Brep solids. It keeps analytical primitives exact
and supports explicitly requested Mesh/SubD deliverables. Document context
includes geometry-type counts and the absolute tolerance in metres. These facts
guide tool selection and stage validation. Source curves, real openings and
decorative panels receive acceptance conditions appropriate to the requested
design scope. Repeated inspection is consolidated around meaningful edit batches.

The embedded page exposes `window.magpie.applyState(state)`.
It sends JSON strings through `window.eto.postMessage`.
In a standalone preview it calls `window.magpiePreviewSend` when provided.

State fields:

- `document`, `status`, `busy`, `canSend`, `canStop`, `draft`, `autoTools`
- `architectural`, `detail` (`concept`, `developed`, `detailed`)
- `models` containing `{id,label}` and `selectedModel`
- `messages` containing `{id,role,type,text,detail,state}`; roles are `user`,
  `assistant`, `tool`, `system`, types are `message`, `tool`, `notice`
- `plan` containing `{goal,steps:[{id,title,acceptance,status}],awaitingReview}`
- `references` containing `{id,name,kind,width}` and `units`

Actions:

- `{action:"ready"}`
- `{action:"send",text:"..."}`
- `{action:"draft",text:"..."}`
- `{action:"stop"}`, `{action:"refresh"}`, `{action:"clear"}`
- `{action:"model",id:"..."}`
- `{action:"options",autoTools:false,architectural:true,detail:"developed"}`
- `{action:"attach"}`, `{action:"removeReference",id:"..."}`
- `{action:"importReference",id:"...",width:6000}`
- `{action:"continue"}`, `{action:"accept"}`

## Acceptance checks

Test fragmented SSE text and tool calls, truncated streams, compilation-error
repair, incomplete-plan continuation and fresh-readback gating. Test the native
panel and reference-scale path in a separate Rhino process. Use assumed test
dimensions, never invent missing project dimensions as settled facts.
