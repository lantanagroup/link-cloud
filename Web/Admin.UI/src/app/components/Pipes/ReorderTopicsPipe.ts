import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'reorderTopics',
  standalone: true,
})
export class ReorderTopicsPipe implements PipeTransform {
  transform(entries: { key: string; value: any[] }[]): { key: string; value: any[] }[] {
    return entries.sort((a, b) => {
      // If key contains "Error", push it to the end
      const isAError = a.key.includes('Error');
      const isBError = b.key.includes('Error');

      if (isAError && !isBError) {
        return 1; // a comes after b
      }
      if (!isAError && isBError) {
        return -1; // b comes after a
      }
      return 0; // no change if both or neither are errors
    });
  }
}
