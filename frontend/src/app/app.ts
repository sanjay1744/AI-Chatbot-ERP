import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { HeaderComponent } from './layout/header/header.component';
import { AiAssistantComponent } from './modules/ai-assistant/ai-assistant.component';
import { CopilotStateService } from './services/copilot-state.service';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SidebarComponent, HeaderComponent, AiAssistantComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  title = 'AriyAI ERP & CRM';
  copilotState = inject(CopilotStateService);
  authService = inject(AuthService);

  toggleCopilot(): void {
    this.copilotState.toggle();
  }
}
