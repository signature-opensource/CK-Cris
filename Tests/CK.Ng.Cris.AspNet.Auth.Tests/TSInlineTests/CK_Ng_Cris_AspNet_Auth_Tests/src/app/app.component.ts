import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CKGenAppModule } from '@local/ck-gen';

const ckGenInjected: CKGenInjected = [];
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CKGenAppModule, ...ckGenInjected],
  templateUrl: './app.component.html',
  styleUrl: './app.component.less'
})
export class AppComponent {
  title = 'CK_Ng_Cris_AspNet_Auth_Tests';
}

