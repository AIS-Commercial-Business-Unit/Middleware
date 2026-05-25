import type { Metadata } from "next";
import "./globals.css";
import Link from "next/link";

export const metadata: Metadata = {
  title: "AIS Middleware Platform",
  description: "UC1: Policy Issuance Demo — Apache Camel + Kafka + MongoDB",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-screen antialiased" style={{ background: "var(--bg)", color: "var(--text)" }}>
        <nav
          className="border-b px-6 py-3 flex items-center gap-8"
          style={{ borderColor: "var(--border)", background: "var(--surface)" }}
        >
          <span className="font-bold text-lg tracking-tight" style={{ color: "var(--accent-light)" }}>
            AIS Middleware
          </span>
          <Link href="/" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
            Issue Policy
          </Link>
          <Link href="/events" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
            Live Events
          </Link>
          <a
            href={process.env.NEXT_PUBLIC_GRAFANA_URL ?? "http://localhost:3001"}
            target="_blank"
            rel="noopener noreferrer"
            className="text-sm hover:text-white transition-colors ml-auto"
            style={{ color: "var(--muted)" }}
          >
            Grafana ↗
          </a>
        </nav>
        <main className="px-6 py-8 max-w-5xl mx-auto">{children}</main>
      </body>
    </html>
  );
}
