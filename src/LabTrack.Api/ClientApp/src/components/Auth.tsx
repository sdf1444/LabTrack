import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link, Navigate, Outlet, useLocation, useNavigate } from "react-router-dom";
import { ApiError, getCurrentUser, getErrorMessage, signIn, signOut } from "../api/client";
import { FocusedHeading } from "./FocusedHeading";
import { z } from "zod";

const currentUserKey = ["auth", "current-user"] as const;

const loginLocationStateSchema = z.object({
  logoutError: z.string().optional(),
});

async function loadCurrentUser(signal: AbortSignal) {
  try {
    return await getCurrentUser(signal);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: ({ signal }) => loadCurrentUser(signal),
  });
}

function SessionLoading() {
  return (
    <main className="login-shell">
      <p className="loading" role="status">
        Loading session…
      </p>
    </main>
  );
}

export function Header({ email, onSignOut }: { email?: string; onSignOut?: () => void }) {
  return (
    <header className="topbar">
      <Link className="brand" to="/">
        <span className="brand-mark" aria-hidden="true">
          L
        </span>
        <span>
          labtrack<span className="brand-dot">.</span>
        </span>
      </Link>
      <div className="topbar-meta">
        <span className="env-dot" /> Demo laboratory <span className="topbar-divider" /> Synthetic
        data
        {email && onSignOut && (
          <>
            <span className="topbar-divider" />
            <span>{email}</span>
            <button className="logout-button" type="button" onClick={onSignOut}>
              Sign out
            </button>
          </>
        )}
      </div>
    </header>
  );
}

export function AuthenticatedRoute() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const userQuery = useCurrentUser();

  if (userQuery.isPending) return <SessionLoading />;
  if (userQuery.isError) {
    return (
      <>
        <Header />
        <main className="login-shell">
          <section className="login-card panel" role="alert">
            <FocusedHeading>Unable to verify your session</FocusedHeading>
            <p className="subtitle">{getErrorMessage(userQuery.error)}</p>
            <Link to="/login">Go to sign in</Link>
          </section>
        </main>
      </>
    );
  }
  if (!userQuery.data) return <Navigate to="/login" replace />;

  async function handleSignOut() {
    let logoutError: string | undefined;
    try {
      await signOut();
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 401)) {
        logoutError = `Sign-out could not be confirmed: ${getErrorMessage(error)}`;
      }
    } finally {
      queryClient.setQueryData(currentUserKey, null);
      navigate("/login", {
        replace: true,
        state: logoutError ? { logoutError } : null,
      });
    }
  }

  return (
    <>
      <Header email={userQuery.data.email} onSignOut={() => void handleSignOut()} />
      <Outlet />
    </>
  );
}

function LoginPage({ initialError }: { initialError?: string }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [error, setError] = useState(initialError ?? "");
  const loginMutation = useMutation({
    mutationFn: ({ email, password }: { email: string; password: string }) =>
      signIn(email, password),
    onSuccess: (user) => {
      queryClient.setQueryData(currentUserKey, user);
      navigate("/", { replace: true });
    },
    onError: (requestError: Error) => setError(requestError.message),
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const email = data.get("email");
    const password = data.get("password");
    if (typeof email !== "string" || typeof password !== "string") {
      setError("Enter your email and password.");
      return;
    }
    setError("");
    loginMutation.mutate({ email, password });
  }

  return (
    <>
      <Header />
      <main className="login-shell">
        <section className="login-card panel">
          <p className="eyebrow">LABORATORY OPERATIONS</p>
          <FocusedHeading>Sign in to LabTrack</FocusedHeading>
          <p className="subtitle">Use the account provided by your LabTrack administrator.</p>
          <form className="login-form" onSubmit={handleSubmit}>
            <label htmlFor="login-email">Email</label>
            <input
              id="login-email"
              name="email"
              type="email"
              autoComplete="username"
              required
              maxLength={256}
            />
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              name="password"
              type="password"
              autoComplete="current-password"
              required
            />
            {error && (
              <p className="login-error" role="alert">
                {error}
              </p>
            )}
            <button
              className="button button-primary"
              type="submit"
              disabled={loginMutation.isPending}
            >
              {loginMutation.isPending ? "Signing in…" : "Sign in"}
            </button>
          </form>
        </section>
      </main>
    </>
  );
}

export function LoginRoute() {
  const location = useLocation();
  const userQuery = useCurrentUser();
  const locationState = loginLocationStateSchema.safeParse(location.state);
  const logoutError = locationState.success ? locationState.data.logoutError : undefined;

  if (userQuery.isPending && !logoutError) return <SessionLoading />;
  if (userQuery.data && !logoutError) return <Navigate to="/" replace />;

  const sessionError = userQuery.isError
    ? `Unable to verify your session: ${getErrorMessage(userQuery.error)}`
    : undefined;

  return <LoginPage initialError={logoutError ?? sessionError} />;
}
