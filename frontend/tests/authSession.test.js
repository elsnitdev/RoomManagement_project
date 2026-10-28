import assert from "node:assert/strict";
import test from "node:test";
import { clearAuthSession, readAuthSession, writeAuthSession } from "../src/contexts/authSession.js";

function memoryStorage() {
  const values = new Map();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key),
  };
}

test("restores an unexpired login after a reload", () => {
  const storage = memoryStorage();
  const session = {
    accessToken: "token",
    expiresAt: new Date(20_000).toISOString(),
    user: { id: "user-1", role: "Landlord" },
  };

  writeAuthSession(session, storage);
  assert.deepEqual(readAuthSession(10_000, storage), session);
});

test("drops expired and malformed saved sessions", () => {
  const storage = memoryStorage();
  writeAuthSession({ accessToken: "token", expiresAt: new Date(10_000).toISOString(), user: { id: "1", role: "Admin" } }, storage);
  assert.equal(readAuthSession(10_000, storage), null);
  assert.equal(readAuthSession(0, storage), null);

  storage.setItem("roomManagement.auth", "not-json");
  assert.equal(readAuthSession(0, storage), null);
  assert.equal(storage.getItem("roomManagement.auth"), null);
});

test("signing out removes the saved session", () => {
  const storage = memoryStorage();
  writeAuthSession({ accessToken: "token" }, storage);
  clearAuthSession(storage);
  assert.equal(readAuthSession(0, storage), null);
});
