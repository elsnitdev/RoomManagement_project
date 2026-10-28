import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { login as loginRequest } from "../services/api";
import { setAccessToken } from "../services/apiClient";
import { invalidateApiCache } from "../services/apiCache";
import { clearAuthSession, readAuthSession, writeAuthSession } from "./authSession";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [session, setSession] = useState(() => {
    const saved = readAuthSession();
    setAccessToken(saved?.accessToken);
    return saved;
  });

  const signOut = useCallback(() => {
    setAccessToken(null);
    clearAuthSession();
    invalidateApiCache();
    setSession(null);
  }, []);

  const signIn = useCallback(async (credentials) => {
    const result = await loginRequest(credentials);
    if (
      !result.accessToken ||
      !result.user?.id ||
      !result.user?.role ||
      !(Date.parse(result.expiresAt) > Date.now())
    ) {
      throw new Error("Phản hồi đăng nhập không hợp lệ.");
    }

    const nextSession = {
      accessToken: result.accessToken,
      expiresAt: result.expiresAt,
      user: result.user,
    };
    invalidateApiCache();
    setAccessToken(result.accessToken);
    writeAuthSession(nextSession);
    setSession(nextSession);
    return result;
  }, []);

  useEffect(() => {
    if (!session) return;
    const remainingMs = Date.parse(session.expiresAt) - Date.now();
    if (remainingMs <= 0) {
      signOut();
      return;
    }
    const timer = setTimeout(signOut, remainingMs);
    return () => clearTimeout(timer);
  }, [session, signOut]);

  useEffect(() => {
    window.addEventListener("auth:unauthorized", signOut);
    return () => window.removeEventListener("auth:unauthorized", signOut);
  }, [signOut]);

  const user = session?.user ?? null;

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: Boolean(user && session?.accessToken), signIn, signOut }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context)
    throw new Error("useAuth phải được sử dụng bên trong AuthProvider.");
  return context;
}
