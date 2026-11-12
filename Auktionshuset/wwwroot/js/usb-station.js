function stationBase(configuredUrl) {
    const url = new URL(configuredUrl);
    if (url.protocol !== "http:" || url.hostname !== "127.0.0.1" ||
        !url.port || url.pathname !== "/" || url.search || url.hash || url.username || url.password)
        throw new Error("USB-stationens adresse er ugyldig.");
    return url.origin;
}

export async function status(configuredUrl) {
    try {
        const base = stationBase(configuredUrl);
        const response = await fetch(`${base}/v1/device`, { cache: "no-store" });
        if (!response.ok) return { state: "unavailable" };
        const device = await response.json();
        if (device.protocolVersion !== 1 || !["disconnected", "connecting", "unconfigured", "configured"].includes(device.state))
            return { state: "unavailable" };
        return device;
    } catch {
        return { state: "unavailable" };
    }
}

async function transfer(configuredUrl, path, registration, connectionId) {
    const base = stationBase(configuredUrl);
    // Credentials exist only in this invocation. Never place them in browser storage or logs.
    const response = await fetch(`${base}${path}`, {
        method: "POST",
        cache: "no-store",
        headers: { "Content-Type": "application/json", "X-Station-Grant": registration.grant },
        body: JSON.stringify({ connectionId, deviceId: registration.deviceId,
            deviceNumber: registration.deviceNumber, secret: registration.secret })
    });
    if (!response.ok) throw new Error("USB-overførslen kunne ikke bekræftes.");
    const result = await response.json();
    if (result.protocolVersion !== 1 || result.state !== "configured" ||
        result.connectionId !== connectionId || result.deviceId !== registration.deviceId ||
        result.deviceNumber !== registration.deviceNumber)
        throw new Error("USB-stationens kvittering stemmer ikke med registreringen.");
    return { deviceId: result.deviceId, deviceNumber: result.deviceNumber };
}

export const provision = (configuredUrl, registration, connectionId) =>
    transfer(configuredUrl, "/v1/provision", registration, connectionId);

export const repair = (configuredUrl, registration, connectionId) =>
    transfer(configuredUrl, "/v1/repair", registration, connectionId);
