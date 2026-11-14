import { Component, type ReactNode } from "react";
import { FocusedHeading } from "./FocusedHeading";

type Props = { children: ReactNode };
type State = { hasError: boolean };

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  render() {
    if (this.state.hasError) {
      return (
        <main className="login-shell" role="alert">
          <section className="login-card panel">
            <FocusedHeading>Something went wrong</FocusedHeading>
            <p className="subtitle">Reload LabTrack to try again.</p>
            <button
              className="button button-primary"
              type="button"
              onClick={() => window.location.reload()}
            >
              Reload
            </button>
          </section>
        </main>
      );
    }

    return this.props.children;
  }
}
