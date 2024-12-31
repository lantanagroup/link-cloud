import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import * as CryptoJS from 'crypto-js';


@Injectable({
  providedIn: 'root'
})
export class SessionStorageService {
  private phrase: string = "randomPhrase"
  private salt: any = CryptoJS.lib.WordArray.random(128 / 8);
  //private sessionKey: any = this.generateKey(this.phrase, this.salt);
  private sessionKey: any = "mySuperDuperSecureKey";

  constructor() { }

  storeItem(key: string, value: any): void {
    sessionStorage.setItem(key, CryptoJS.AES.encrypt(value, this.sessionKey).toString())

    //if not in production, also store if in plain text
    if (!environment.production) {
      sessionStorage.setItem(key.concat("-dev"), value);
    }
    
  }

  getItem(key: string) {
    let value = sessionStorage.getItem(key);
    if (value) {
      return CryptoJS.AES.decrypt(value, this.sessionKey).toString(CryptoJS.enc.Utf8)
      //return value;
    } else {
      return null
    }
  }

  removeItem(key: string): void {
    sessionStorage.removeItem(key);
    if (!environment.production) {
      sessionStorage.removeItem(key.concat("-dev"));
    }
  }

  clearSession(): void {
    sessionStorage.clear();
  }

  private generateKey(pass: string, salt: any) {
    return CryptoJS.PBKDF2(pass, salt, { keySize: 512 / 32, iterations: 1000 });
  }
  
  
}
