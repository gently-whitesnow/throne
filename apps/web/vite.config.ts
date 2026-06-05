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
        manualChunks(id) {
          if (!id.includes("node_modules")) return undefined;
          if (id.includes("@xterm")) return "xterm";
          if (id.includes("react-router")) return "router";
          if (
            id.includes("react-dom") ||
            id.includes("/scheduler/") ||
            id.includes("node_modules/react/")
          ) {
            return "react";
          }
          if (id.includes("@tanstack")) return "tanstack";
          return "vendor";
        }
      }
    }
  },
  server: {
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY_TARGET ?? "http://127.0.0.1:5008",
        changeOrigin: true
      }
    }
  },
  test: {
    environment: "jsdom"
  }
});
