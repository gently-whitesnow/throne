import { QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { BrowserRouter, Route, Routes } from "react-router-dom";

import { HomePage } from "@/pages/home";
import { ImprovementsSectionPage } from "@/pages/improvements-section";
import { IntentDetailPage } from "@/pages/intent-detail";
import { IntentsSectionPage } from "@/pages/intents-section";
import { InstructionsSectionPage } from "@/pages/instructions-section";
import { RepositoryDetailPage } from "@/pages/repository-detail";
import { RepositoriesSectionPage } from "@/pages/repositories-section";
import { SettingsPage } from "@/pages/settings";
import { TagsSectionPage } from "@/pages/tags-section";
import { AppShell } from "@/widgets/app-shell";

import { createQueryClient } from "./query-client";
import { RealtimeQueryBridge } from "./realtime-query-bridge";

export function App() {
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <RealtimeQueryBridge />
      <BrowserRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/intents" element={<IntentsSectionPage />}>
              <Route path=":id" element={<IntentDetailPage />} />
            </Route>
            <Route path="/tags" element={<TagsSectionPage />} />
            <Route path="/repositories" element={<RepositoriesSectionPage />} />
            <Route
              path="/repositories/:provider/:owner/:repo"
              element={<RepositoryDetailPage />}
            />
            <Route path="/instructions" element={<InstructionsSectionPage />} />
            <Route path="/improvements" element={<ImprovementsSectionPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
