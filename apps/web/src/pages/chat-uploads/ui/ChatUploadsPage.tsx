import { ChatUploadsList } from "@/widgets/chat-uploads-list";

export function ChatUploadsPage() {
  return (
    <section className="flex h-screen min-w-0 flex-col">
      <header className="border-b border-base-300 bg-base-200 px-4 py-3">
        <h1 className="m-0 text-lg font-semibold">Chat uploads</h1>
        <p className="m-0 mt-1 text-xs text-base-content/60">
          Архивы переписок с агентами, загруженные для обучения системы.
          Загружаются из чата с агентом (mode=transfer); UI — только просмотр и
          управление.
        </p>
      </header>
      <main className="min-h-0 flex-1 overflow-auto">
        <ChatUploadsList />
      </main>
    </section>
  );
}
