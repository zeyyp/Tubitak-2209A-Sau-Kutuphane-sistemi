import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './contact.component.html',
  styleUrls: ['./contact.component.css']
})
export class ContactComponent {
  contactForm = {
    name: '',
    email: '',
    message: ''
  };

  onSubmit() {
    console.log('Form submitted', this.contactForm);
    alert('Mesajınız alındı. Teşekkür ederiz!');
    // Form'u resetle
    this.contactForm = {
      name: '',
      email: '',
      message: ''
    };
  }
}
