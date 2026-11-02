export function introDismissed() { try { return localStorage.getItem('sain.botSelection.intro') === '1'; } catch { return false; } }
export function dismissIntro() { try { localStorage.setItem('sain.botSelection.intro', '1'); } catch { } }
export async function copy(text) { await navigator.clipboard.writeText(text); }
export function selectCode(element) { element?.focus(); element?.select(); }
