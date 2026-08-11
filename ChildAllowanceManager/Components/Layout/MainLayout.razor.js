export function createCookie(name, value, days) {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie =
        `${name}=${encodeURIComponent(value)}; expires=${expires}; path=/; SameSite=Lax`;
}
