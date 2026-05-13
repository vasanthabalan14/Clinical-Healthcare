import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [RouterLink],
  template: `
    <nav class="navbar"><span class="logo">ClinicalHub</span></nav>
    <main class="main"><div class="card"><h1>Reset password</h1><p><a routerLink="/login">Back to sign in</a></p></div></main>
  `,
  styles: [`
    .navbar{height:var(--nh);background:var(--cs0);border-bottom:1px solid var(--cb);display:flex;align-items:center;padding:0 var(--sp6);position:fixed;top:0;left:0;right:0;z-index:300;}
    .logo{font-size:18px;font-weight:600;color:var(--cp);}
    .main{display:flex;align-items:center;justify-content:center;min-height:100vh;padding-top:var(--nh);}
    .card{background:var(--cs0);border:1px solid var(--cb);border-radius:var(--r2);padding:var(--sp8);max-width:400px;width:100%;}
  `]
})
export class ForgotPasswordComponent {}
