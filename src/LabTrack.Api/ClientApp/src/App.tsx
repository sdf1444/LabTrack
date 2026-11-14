import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AuthenticatedRoute, LoginRoute } from "./components/Auth";

const SampleDashboard = lazy(() =>
  import("./components/SampleDashboard").then((module) => ({ default: module.SampleDashboard })),
);

function DashboardLoading() {
  return (
    <main className="shell">
      <p className="loading" role="status">
        Loading dashboard…
      </p>
    </main>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginRoute />} />
      <Route element={<AuthenticatedRoute />}>
        <Route
          index
          element={
            <Suspense fallback={<DashboardLoading />}>
              <SampleDashboard />
            </Suspense>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
