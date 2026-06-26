import path from "node:path";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [tailwindcss(), react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  },
  build: {
    rollupOptions: {
      output: {
        // Тяжёлые vendor-зависимости разводим по отдельным чанкам: один общий
        // бандл рос выше порога Vite и инвалидировался при любой правке UI.
        // ВАЖНО: React и всё, что вызывает React-API (createContext) на этапе
        // инициализации модуля — tanstack, react-router — держим в ОДНОМ чанке.
        // Разнесение по разным чанкам ломает порядок исполнения в проде
        // (tanstack-чанк дёргает React раньше, чем инициализирован react-чанк) →
        // "Cannot read properties of undefined (reading 'createContext')".
        manualChunks(id) {
          if (!id.includes("node_modules")) return undefined;
          if (id.includes("@xterm")) return "xterm";
          if (
            id.includes("react-dom") ||
            id.includes("react-router") ||
            id.includes("@tanstack") ||
            id.includes("/scheduler/") ||
            id.includes("node_modules/react/")
          ) {
            return "react-vendor";
          }
          return "vendor";
        }
      }
    }
  },
  server: {
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY_TARGET ?? "http://127.0.0.1:5008",
        changeOrigin: true,
        // Terminal bridge (/api/v1/intents/{id}/terminal/ws) is a WebSocket;
        // without ws the dev proxy 426s the upgrade and the embedded terminal
        // never connects under `pnpm dev`.
        ws: true
      }
    }
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/app/test-setup.ts"]
  }
});
