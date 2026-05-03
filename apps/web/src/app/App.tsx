import { BrowserRouter, Route, Routes } from "react-router-dom";

import { DreamSectionPage } from "@/pages/dream-section";
import { HomePage } from "@/pages/home";
import { IntentDetailPage } from "@/pages/intent-detail";
import { IntentsSectionPage } from "@/pages/intents-section";
import { InstructionsSectionPage } from "@/pages/instructions-section";
import { TagsSectionPage } from "@/pages/tags-section";
import { AppShell } from "@/widgets/app-shell";

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/intents" element={<IntentsSectionPage />}>
            <Route path=":id" element={<IntentDetailPage />} />
          </Route>
          <Route path="/tags" element={<TagsSectionPage />} />
          <Route path="/instructions" element={<InstructionsSectionPage />} />
          <Route path="/dream" element={<DreamSectionPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
