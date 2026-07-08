import { defineConfig } from "steiger";
import fsd from "@feature-sliced/steiger-plugin";

export default defineConfig([
  ...fsd.configs.recommended,
  {
    // Rationale (CLAUDE.md требует обоснование на ослабление гейта): правило
    // флагует ~35 виджетов, на которые ссылается ровно одна страница, и советует
    // «слить со страницей». В нашей раскладке виджет, скомпонованный в одну
    // страницу, — намеренная FSD-композиция (widget = переиспользуемый блок
    // композиции, не дубль страницы), а совет противоречит слоистой структуре.
    // Правило advisory-шумит на идиоме, поэтому off, а не warn (иначе 35 warnings
    // на каждый прогон гейта глушат полезный сигнал). Реальные «висяки» (slice без
    // ссылок) ловятся при ревью, а не этим правилом.
    rules: {
      "fsd/insignificant-slice": "off"
    }
  },
  {
    ignores: ["**/*.test.ts", "**/*.test.tsx"]
  }
]);
