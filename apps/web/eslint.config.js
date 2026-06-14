import js from "@eslint/js";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";

// Запрет сырых hex-цветов и Tailwind arbitrary-цветов в коде: единственный
// источник палитры — DESIGN.md → tokens.generated.css, потребляется через
// var(--color-*). app/styles (CSS) не линтится ESLint, поэтому правило глобально
// для .ts/.tsx. См. DESIGN.md: ручная транскрипция OKLCH→hex запрещена.
const NO_RAW_COLORS = [
  {
    selector: "Literal[value=/#[0-9a-fA-F]{3,8}\\b/]",
    message:
      "Сырой hex-цвет запрещён. Используйте дизайн-токен var(--color-*) (источник — DESIGN.md)."
  },
  {
    selector: "TemplateElement[value.raw=/#[0-9a-fA-F]{3,8}\\b/]",
    message:
      "Сырой hex-цвет запрещён. Используйте дизайн-токен var(--color-*) (источник — DESIGN.md)."
  },
  {
    selector: "Literal[value=/(?:bg|text|border)-\\[#/]",
    message:
      "Tailwind arbitrary-цвет (bg|text|border)-[#…] запрещён. Используйте токен-класс или var(--color-*)."
  },
  {
    selector: "TemplateElement[value.raw=/(?:bg|text|border)-\\[#/]",
    message:
      "Tailwind arbitrary-цвет (bg|text|border)-[#…] запрещён. Используйте токен-класс или var(--color-*)."
  }
];

// instanceof HttpError размазывает обработку ошибок по UI-слоям. В
// features/widgets/pages статус/код/сообщение читаются через хелперы
// @/shared/lib (httpErrorStatus / httpErrorCode / errorMessage).
const NO_HTTP_ERROR_INSTANCEOF = {
  selector: "BinaryExpression[operator='instanceof'][right.name='HttpError']",
  message:
    "instanceof HttpError вне shared/entities запрещён. Используйте errorMessage / httpErrorStatus / httpErrorCode из @/shared/lib."
};

export default tseslint.config(
  {
    ignores: ["dist", "src/shared/api/generated/**"]
  },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      parserOptions: {
        project: ["./tsconfig.app.json", "./tsconfig.node.json"],
        tsconfigRootDir: import.meta.dirname
      }
    },
    plugins: {
      "react-hooks": reactHooks,
      "react-refresh": reactRefresh
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      "no-undef": "off",
      "react-refresh/only-export-components": [
        "warn",
        { allowConstantExport: true }
      ],
      "no-restricted-syntax": ["error", ...NO_RAW_COLORS]
    }
  },
  // createPortal живёт только в shared/ui (Modal). Везде остальное — через <Modal>.
  {
    files: ["**/*.{ts,tsx}"],
    ignores: ["src/shared/ui/**"],
    rules: {
      "no-restricted-imports": [
        "error",
        {
          paths: [
            {
              name: "react-dom",
              importNames: ["createPortal"],
              message:
                "createPortal только в shared/ui. Используйте <Modal> из @/shared/ui."
            }
          ]
        }
      ]
    }
  },
  // В UI-слоях — плюс запрет instanceof HttpError (повторяем NO_RAW_COLORS, т.к.
  // flat config заменяет правило целиком для совпавших файлов, а не мёржит).
  {
    files: [
      "src/features/**/*.{ts,tsx}",
      "src/widgets/**/*.{ts,tsx}",
      "src/pages/**/*.{ts,tsx}"
    ],
    rules: {
      "no-restricted-syntax": [
        "error",
        ...NO_RAW_COLORS,
        NO_HTTP_ERROR_INSTANCEOF
      ]
    }
  }
);
