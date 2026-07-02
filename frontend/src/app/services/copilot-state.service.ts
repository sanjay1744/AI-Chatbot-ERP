import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class CopilotStateService {
  isOpen = signal<boolean>(false);

  toggle(): void {
    this.isOpen.update(val => !val);
  }

  open(): void {
    this.isOpen.set(true);
  }

  close(): void {
    this.isOpen.set(false);
  }
}
