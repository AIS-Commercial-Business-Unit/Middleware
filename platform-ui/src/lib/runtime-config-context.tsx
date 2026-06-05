"use client";

import { createContext, useContext, ReactNode } from "react";

interface RuntimeConfig {
  grafanaUrl: string;
  kafdropUrl: string;
}

const RuntimeConfigContext = createContext<RuntimeConfig>({
  grafanaUrl: "http://localhost:3001",
  kafdropUrl: "http://localhost:9000",
});

export function RuntimeConfigProvider({
  config,
  children,
}: {
  config: RuntimeConfig;
  children: ReactNode;
}) {
  return (
    <RuntimeConfigContext.Provider value={config}>
      {children}
    </RuntimeConfigContext.Provider>
  );
}

export function useRuntimeConfig(): RuntimeConfig {
  return useContext(RuntimeConfigContext);
}
