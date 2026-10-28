const sessionKey = "roomManagement.auth";

function browserSessionStorage() {
  try {
    return globalThis.sessionStorage ?? null;
  } catch {
    return null;
  }
}

export function readAuthSession(now = Date.now(), storage = browserSessionStorage()) {
  if (!storage) return null;

  try {
    const raw = storage.getItem(sessionKey);
    if (!raw) return null;

    const session = JSON.parse(raw);
    // console.log(session);
    if (
      typeof session?.accessToken === "string" &&
      session.accessToken.length > 0 &&
      typeof session.user?.id === "string" &&
      typeof session.user?.role === "string" &&
      Number.isFinite(Date.parse(session.expiresAt)) &&
      Date.parse(session.expiresAt) > now
    ) {
      return session;
    }

    storage.removeItem(sessionKey);
  } catch {
    try {
      storage.removeItem(sessionKey);
    } catch {
      // Storage may be unavailable in a restricted browser context.
    }
  }

  return null;
}

export function writeAuthSession(session, storage = browserSessionStorage()) {
  try {
    storage?.setItem(sessionKey, JSON.stringify(session));
  } catch {
    // Login still works in memory if the browser blocks sessionStorage.
  }
}

export function clearAuthSession(storage = browserSessionStorage()) {
  try {
    storage?.removeItem(sessionKey);
  } catch {
    // Clearing the in-memory session remains possible.
  }
}
