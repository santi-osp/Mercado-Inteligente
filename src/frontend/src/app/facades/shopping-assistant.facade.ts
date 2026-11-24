import { Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import {
  AssistantProductDto,
  AssistantProductRecommendationDto,
  ShoppingAssistantRequest,
  ShoppingAssistantResponse
} from "../models";

export type ChatMessage = {
  sender: "user" | "assistant";
  text: string;
  recommendedProducts?: AssistantProductRecommendationDto[];
  timestamp: Date;
};

@Injectable({ providedIn: "root" })
export class ShoppingAssistantFacade {
  private readonly http = inject(HttpClient);

  readonly isOpen = signal(false);
  readonly messages = signal<ChatMessage[]>([]);
  readonly loading = signal(false);
  readonly error = signal("");
  readonly status = signal("");
  readonly modelUsed = signal("");

  open() {
    this.isOpen.set(true);
  }

  close() {
    this.isOpen.set(false);
  }

  toggleOpen() {
    this.isOpen.update(v => !v);
  }

  toggle() {
    this.toggleOpen();
  }

  clearMessages() {
    this.messages.set([]);
  }

  clearHistory() {
    this.clearMessages();
  }

  sendMessage(
    text: string,
    pageContext: string,
    visibleProducts?: AssistantProductDto[],
    basketId?: string | null
  ) {
    const trimmed = text.trim();
    if (!trimmed || this.loading()) return;

    this.messages.update(prev => [
      ...prev,
      { sender: "user", text: trimmed, timestamp: new Date() }
    ]);

    this.loading.set(true);
    this.error.set("");

    const requestPayload: ShoppingAssistantRequest = {
      message: trimmed,
      pageContext: pageContext || "search",
      visibleProducts: visibleProducts && visibleProducts.length > 0 ? visibleProducts.slice(0, 15) : undefined,
      basketId: basketId || null
    };

    this.http.post<ShoppingAssistantResponse>("/api/v1/ai/shopping-assistant", requestPayload).subscribe({
      next: res => {
        this.status.set(res.status);
        if (res.modelUsed) this.modelUsed.set(res.modelUsed);
        this.messages.update(prev => [
          ...prev,
          {
            sender: "assistant",
            text: res.reply,
            recommendedProducts: res.recommendedProducts,
            timestamp: new Date()
          }
        ]);
        this.loading.set(false);
      },
      error: () => {
        this.messages.update(prev => [
          ...prev,
          {
            sender: "assistant",
            text: "No pude consultar la IA en este momento. Tus precios y listas siguen disponibles.",
            timestamp: new Date()
          }
        ]);
        this.loading.set(false);
      }
    });
  }
}
