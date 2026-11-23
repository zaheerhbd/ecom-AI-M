import { Pipe, PipeTransform } from '@angular/core';

/** Escapes text so it can be put inside HTML safely. */
function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * Turns the small amount of Markdown the model writes (**bold**, *italic*, `code`,
 * "* " / "- " bullet lists, numbered lists) into HTML.
 * The text is escaped FIRST, so the model can never inject its own HTML;
 * Angular's [innerHTML] sanitiser is a second safety net.
 */
@Pipe({ name: 'agentMarkdown' })
export class AgentMarkdownPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const lines = escapeHtml(value).split(/\r?\n/);
    const html: string[] = [];
    let list: 'ul' | 'ol' | null = null;

    const closeList = () => {
      if (list) {
        html.push('</' + list + '>');
        list = null;
      }
    };

    for (const raw of lines) {
      const line = raw.trim();
      const bullet = /^[*-]\s+(.*)$/.exec(line);
      const numbered = /^\d+[.)]\s+(.*)$/.exec(line);

      if (bullet || numbered) {
        const kind = bullet ? 'ul' : 'ol';
        if (list !== kind) {
          closeList();
          html.push('<' + kind + '>');
          list = kind;
        }
        html.push('<li>' + inline((bullet || numbered)[1]) + '</li>');
        continue;
      }

      closeList();
      if (line.length > 0) {
        html.push('<p>' + inline(line) + '</p>');
      }
    }

    closeList();
    return html.join('');
  }
}

/** Bold, italic and code inside one line (the text is already escaped). */
function inline(text: string): string {
  return text
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/(^|[^*])\*([^*\s][^*]*?)\*(?!\*)/g, '$1<em>$2</em>')
    .replace(/`([^`]+)`/g, '<code>$1</code>');
}

/** Pretty-prints JSON text (tool inputs and outputs). Anything that isn't JSON is returned unchanged. */
@Pipe({ name: 'prettyJson' })
export class PrettyJsonPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    const trimmed = value.trim();
    if (!(trimmed.startsWith('{') || trimmed.startsWith('['))) {
      return value;
    }
    try {
      return JSON.stringify(JSON.parse(trimmed), null, 2);
    } catch {
      return value;
    }
  }
}
