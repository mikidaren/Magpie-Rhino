import assert from "node:assert/strict";
import test from "node:test";
import { agent, connect } from "../Magpie-Rhino/rhino9-ai.agent.js";

test("uses the product name rather than the local folder label", () => {
  assert.equal(agent.name, "Magpie-Rhino");
});

test("owns a dedicated JSON config and a distinct usage identity", () => {
  assert.equal(agent.id, "rhino9-ai");
  assert.equal(agent.config, "~/.config/rhino9-magpie/settings.json");
  assert.equal(agent.model, "model");
  assert.equal(agent.format, "json");
});

test("preserves gateway model IDs and the root for direct OpenAI-compatible calls", () => {
  const result = connect({
    gateway: { url: "http://127.0.0.1:4567", v1: "http://127.0.0.1:4567/v1", key: "test-key" },
    model: { id: "provider/model/with/slashes" },
  });
  assert.deepEqual(result, { baseUrl: "http://127.0.0.1:4567", apiKey: "test-key" });
});

test("rejects incomplete selections without writing partial config", () => {
  for (const input of [{}, { gateway: {} }, { gateway: { url: "http://127.0.0.1:3425", key: "key" } }]) {
    assert.throws(() => connect(input));
  }
});
