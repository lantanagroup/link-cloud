import { IThemeOption } from "../interfaces/theme-option.interface";

export class ThemeOption implements IThemeOption {
    backgroundColor!: string;
    buttonColor!: string;
    headingColor!: string;
    label!: string;
    value!: string;
}
