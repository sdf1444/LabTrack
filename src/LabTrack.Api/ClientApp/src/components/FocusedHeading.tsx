import { useEffect, useRef, type ReactNode } from "react";

export function FocusedHeading({ children }: { children: ReactNode }) {
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    headingRef.current?.focus();
  }, []);

  return (
    <h1 ref={headingRef} tabIndex={-1}>
      {children}
    </h1>
  );
}
