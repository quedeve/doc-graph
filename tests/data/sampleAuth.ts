export interface UserProfile {
    id: string;
    username: string;
    roles: string[];
}

export class TokenVerifier {
    private secretKey: string;

    constructor(secretKey: string) {
        this.secretKey = secretKey;
    }

    public verify(token: string): boolean {
        return token.length > 10;
    }
}

export function parseJwt(jwtToken: string): Record<string, unknown> {
    return { token: jwtToken };
}
