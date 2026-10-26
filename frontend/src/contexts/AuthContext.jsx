import { createContext, useCallback, useContext, useState } from "react";
import { login as loginRequest } from "../services/api";
import { setAccessToken } from "../services/apiClient";
import { invalidateApiCache } from "../services/apiCache";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);

  const signIn = useCallback(async (credentials) => {
    const result = await loginRequest(credentials);
    invalidateApiCache();
    setAccessToken(result.accessToken);
    setUser(result.user);
    return result;
  }, []);

  const signOut = useCallback(() => {
    setAccessToken(null);
    invalidateApiCache();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: Boolean(user), signIn, signOut }}
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
