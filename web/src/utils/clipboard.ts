/** Copies text, falling back to execCommand where the Clipboard API is missing (plain HTTP on the LAN). */
export function copyText(text: string): boolean {
  if (navigator.clipboard && window.isSecureContext) {
    navigator.clipboard.writeText(text)
    return true
  }
  const textArea = document.createElement('textarea')
  textArea.value = text
  textArea.style.position = 'fixed'
  textArea.style.left = '-999999px'
  textArea.style.top = '-999999px'
  document.body.appendChild(textArea)
  textArea.focus()
  textArea.select()
  let success = false
  try {
    document.execCommand('copy')
    success = true
  } catch (err) {
    console.error('Fallback copy failed', err)
  }
  textArea.remove()
  return success
}
