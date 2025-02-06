import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  ViewEncapsulation,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule, MatIconRegistry } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NgFor } from '@angular/common';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { Subscription } from 'rxjs';
import { map } from 'rxjs/operators';
import { DomSanitizer } from '@angular/platform-browser';
import { LiveAnnouncer } from '@angular/cdk/a11y';
import { StyleManagerService } from '../../../services/style-manager-service';
import { DocsSiteTheme, ThemeStorage } from './theme-storage/theme-storage';

@Component({
  selector: 'theme-picker',
  templateUrl: 'theme-picker.component.html',
  styleUrls: ['theme-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  standalone: true,
  imports: [MatButtonModule, MatTooltipModule, MatMenuModule, MatIconModule, NgFor]
})
export class ThemePickerComponent implements OnInit, OnDestroy {
  private _queryParamSubscription = Subscription.EMPTY;
  currentTheme: DocsSiteTheme | undefined;

  // The below colors need to align with the themes defined in theme-picker.scss
  themes: DocsSiteTheme[] = [
    {
      primary: '#3F51B5',
      accent: '#E91E63',
      displayName: 'Blue & Rose',
      name: 'blue-rose',
      isDark: false
    },
    //{
    //  primary: '#E91E63',
    //  accent: '#607D8B',
    //  displayName: 'Pink & Blue-grey',
    //  name: 'pink-bluegrey',
    //  isDark: true,
    //},
    {
      primary: '#9C27B0',
      accent: '#4CAF50',
      displayName: 'Purple & Green',
      name: 'purple-green',
      isDark: true,
    },
    {
      primary: '#398eb4',
      accent: '#6039b4',
      displayName: 'Zelda Blue',
      name: 'zelda-blue',
      isDark: false,
      isDefault: true
    },
    {
      primary: '#398eb4',
      accent: '#6039b4',
      displayName: 'Zelda Blue Dark',
      name: 'zelda-blue-dark',
      isDark: true
    }
  ];

  constructor(
    public styleManager: StyleManagerService,
    private _themeStorage: ThemeStorage,
    private _activatedRoute: ActivatedRoute,
    private liveAnnouncer: LiveAnnouncer,
    iconRegistry: MatIconRegistry,
    sanitizer: DomSanitizer) {
    iconRegistry.addSvgIcon('theme-example',
      sanitizer.bypassSecurityTrustResourceUrl(
        'assets/img/theme-demo-icon.svg'));
    const themeName = this._themeStorage.getStoredThemeName();
    if (themeName) {
      this.selectTheme(themeName);
    } else {
      this.themes.find(themes => {
        if (themes.isDefault === true) {
          this.selectTheme(themes.name);
        }
      });
    }
  }

  ngOnInit() {
    this._queryParamSubscription = this._activatedRoute.queryParamMap
      .pipe(map((params: ParamMap) => params.get('theme')))
      .subscribe((themeName: string | null) => {
        if (themeName) {
          this.selectTheme(themeName);
        }
      });
  }

  ngOnDestroy() {
    this._queryParamSubscription.unsubscribe();
  }

  selectTheme(themeName: string) {
    const theme = this.themes.find(currentTheme => currentTheme.name === themeName);

    if (!theme) {
      return;
    }

    let prevThemeName = this._themeStorage.getStoredThemeName();
    this.currentTheme = theme;

    //if (theme.isDefault) {
    //  this.styleManager.removeStyle('theme');
    //} else {
    //  this.styleManager.setStyle('theme', `${theme.name}.css`);
    //}

    //this.styleManager.setStyle('theme', `${theme.name}.css`);

    const body = document.getElementsByTagName('body')[0];
    if (prevThemeName) {
      body.classList.remove(prevThemeName);
    }
    body.classList.add(this.currentTheme.name);


    if (this.currentTheme) {
      this.liveAnnouncer.announce(`${theme.displayName} theme selected.`, 'polite', 3000);
      this._themeStorage.storeTheme(this.currentTheme);
    }
  }
}
