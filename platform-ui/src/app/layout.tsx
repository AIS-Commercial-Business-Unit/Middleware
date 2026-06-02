import type { Metadata } from "next";
import "./globals.css";
import Link from "next/link";
import { BackendSwitcher } from "@/components/BackendSwitcher";

export const metadata: Metadata = {
  title: "AIS Middleware Platform",
  description: "UC1: Policy Issuance Demo — Apache Camel + Kafka + MongoDB",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  const grafanaUrl = process.env.NEXT_PUBLIC_GRAFANA_URL ?? "http://localhost:3001";

  return (
    <html lang="en">
      <head>
        <script
          dangerouslySetInnerHTML={{
            __html: `if(!crypto.randomUUID){crypto.randomUUID=function(){return'10000000-1000-4000-8000-100000000000'.replace(/[018]/g,function(c){return(+c^crypto.getRandomValues(new Uint8Array(1))[0]&15>>+c/4).toString(16)})}}`,
          }}
        />
      </head>
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
          <Link href="/ops" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
            Operations
          </Link>
          <Link href="/events" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
            Live Events
          </Link>
          <Link href="/batches" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
            Batches
          </Link>
          <Link href="/uc4" className="text-sm hover:text-white transition-colors" style={{ color: "var(--muted)" }}>
            Appraisals
          </Link>
          <Link href="/demo-control" className="text-sm hover:text-white transition-colors font-semibold" style={{ color: "#f59e0b" }}>
            🎛️ Demo
          </Link>
          <div className="ml-auto flex items-center gap-4">
            <a
              href={grafanaUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="text-sm hover:text-white transition-colors"
              style={{ color: "var(--muted)" }}
            >
              Grafana ↗
            </a>
            <BackendSwitcher />
          </div>
        </nav>
        <main className="px-6 py-8 max-w-[1800px] mx-auto">{children}</main>
      </body>
    </html>
  );
}
