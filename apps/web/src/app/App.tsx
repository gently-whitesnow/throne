import { QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import { AgentContextPage } from "@/pages/agent-context";
import { HomePage } from "@/pages/home";
import { IntentDetailPage } from "@/pages/intent-detail";
import { IntentsSectionPage } from "@/pages/intents-section";
import { SettingsPage } from "@/pages/settings";
import { StartPage } from "@/pages/start";
import { TagsSectionPage } from "@/pages/tags-section";
import { AppShell } from "@/widgets/app-shell";
import { ReviewWorkspaceRoute } from "@/widgets/review-workspace";

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
            <Route path="/start" element={<StartPage />} />
            <Route path="/intents" element={<IntentsSectionPage />}>
              <Route path=":id" element={<IntentDetailPage />}>
                <Route
                  path="review/:bindingId"
                  element={<ReviewWorkspaceRoute />}
                />
              </Route>
            </Route>
            <Route path="/tags" element={<TagsSectionPage />} />
            <Route path="/agent-context" element={<AgentContextPage />} />
            <Route
              path="/instructions"
              element={<Navigate to="/agent-context" replace />}
            />
            <Route
              path="/improvements"
              element={<Navigate to="/agent-context" replace />}
            />
            <Route
              path="/launch-skills"
              element={<Navigate to="/agent-context" replace />}
            />
            <Route path="/settings" element={<SettingsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
