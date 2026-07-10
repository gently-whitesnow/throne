import { useId } from "react";
import { Link } from "react-router-dom";

import { Button, Modal } from "@/shared/ui";

import { useAttachCardForm } from "../model/use-attach-card-form";
import { CardSearchCombobox } from "./CardSearchCombobox";

interface AttachCardModalProps {
  intentId: string;
  open: boolean;
  onClose: () => void;
}

const TITLE_ID = "attach-card-modal-title";

/**
 * Модал attach: трекер + доска из board-selection задают координату, оператор
 * ищет карточку combobox'ом (пустой фокус = недавние; ввод текста = поиск по
 * названию), либо приклеивает по чистому id как коротким путём.
 */
export function AttachCardModal({
  intentId,
  open,
  onClose
}: AttachCardModalProps) {
  const form = useAttachCardForm(intentId, onClose);
  const cardLabelId = useId();

  if (!open) return null;

  const hasTrackers = form.connectedTrackers.length > 0;

  return (
    <Modal onClose={onClose} labelledBy={TITLE_ID} boxClassName="max-w-md">
      <h2
        id={TITLE_ID}
        className="m-0 text-base font-semibold text-base-content"
      >
        Приложить карточку
      </h2>

      {!hasTrackers ? (
        <p className="mt-3 text-sm text-base-content/70">
          Нет подключённых трекеров с досками.{" "}
          <Link to="/settings" className="link link-primary" onClick={onClose}>
            Подключите трекер в настройках
          </Link>
          , затем приложите карточку по id.
        </p>
      ) : (
        <form
          className="mt-3 flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            void form.submit();
          }}
        >
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-base-content/70">Трекер</span>
            <select
              className="select select-bordered select-sm"
              value={form.tracker}
              onChange={(event) => {
                form.setTracker(event.target.value);
              }}
            >
              {form.connectedTrackers.map((connection) => (
                <option key={connection.tracker} value={connection.tracker}>
                  {connection.display_name}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span className="text-base-content/70">Доска</span>
            <select
              className="select select-bordered select-sm"
              value={form.boardId}
              disabled={form.boardsLoading || form.boards.length === 0}
              onChange={(event) => {
                form.setBoardId(event.target.value);
              }}
            >
              {form.boards.length === 0 ? (
                <option value="">Нет выбранных досок</option>
              ) : (
                form.boards.map((board) => (
                  <option key={board.board_id} value={board.board_id}>
                    {board.space_title ? `${board.space_title} / ` : ""}
                    {board.board_title ?? board.board_id}
                  </option>
                ))
              )}
            </select>
          </label>

          <div className="flex flex-col gap-1 text-sm">
            <span id={cardLabelId} className="text-base-content/70">
              ID карточки
            </span>
            <CardSearchCombobox
              tracker={form.tracker}
              boardId={form.boardId}
              value={form.cardId}
              onValueChange={form.setCardId}
              onSelect={(card) => {
                form.setCardId(card.card_id);
              }}
              labelId={cardLabelId}
              disabled={form.boardId.length === 0}
            />
          </div>

          {form.error ? (
            <p role="alert" className="m-0 text-sm text-error">
              {form.error}
            </p>
          ) : null}

          <div className="mt-1 flex justify-end gap-2">
            <Button type="button" onClick={onClose}>
              Отмена
            </Button>
            <Button type="submit" variant="primary" disabled={!form.canSubmit}>
              {form.submitting ? "Прикладываем…" : "Приложить"}
            </Button>
          </div>
        </form>
      )}
    </Modal>
  );
}
