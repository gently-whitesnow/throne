import { BrowserRouter, Route, Routes } from "react-router-dom";

import { HomePage } from "@/pages/home";
import { IntentDetailPage } from "@/pages/intent-detail";
import { InstructionDetailPage } from "@/pages/instruction-detail";

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/intents/:id" element={<IntentDetailPage />} />
        <Route path="/instructions/:id" element={<InstructionDetailPage />} />
      </Routes>
    </BrowserRouter>
  );
}
