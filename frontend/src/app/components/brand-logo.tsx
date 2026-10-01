import { cn } from "../lib/utils"

interface BrandLogoProps {
    /** Show the "SCRAPGO" wordmark next to the hexagon mark. */
    withWordmark?: boolean
    /** Render the wordmark in light text (for dark backgrounds). */
    inverted?: boolean
    /** Show the "DOWNSTREAM" product tagline beneath the wordmark. */
    withTagline?: boolean
    className?: string
    /** Height of the hexagon mark in pixels. */
    size?: number
}

/**
 * ScrapGo brand lockup — hexagonal scrap-metal badge with the green accent
 * panel, matching the mark used on scrapgo.com.
 */
export function BrandLogo({
    withWordmark = true,
    inverted = false,
    withTagline = false,
    className,
    size = 36,
}: BrandLogoProps) {
    return (
        <span className={cn("inline-flex items-center gap-2.5", className)}>
            <BrandMark size={size} />
            {withWordmark && (
                <span className="flex flex-col leading-none">
                    <span
                        className={cn(
                            "font-display text-[1.35rem] leading-none tracking-tight",
                            inverted ? "text-white" : "text-ink-900"
                        )}
                    >
                        <span className="font-light">SCRAP</span>
                        <span className="font-extrabold">GO</span>
                    </span>
                    {withTagline && (
                        <span
                            className={cn(
                                "mt-1 text-[0.5625rem] font-semibold uppercase tracking-[0.28em]",
                                inverted ? "text-brand-400" : "text-brand-600"
                            )}
                        >
                            Downstream
                        </span>
                    )}
                </span>
            )}
        </span>
    )
}

/** The hexagon mark on its own. */
export function BrandMark({ size = 36, className }: { size?: number; className?: string }) {
    return (
        <svg
            width={size}
            height={size}
            viewBox="0 0 64 64"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            className={cn("shrink-0", className)}
            role="img"
            aria-label="ScrapGo"
        >
            <defs>
                <linearGradient id="bm-green" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0" stopColor="#4ecb3a" />
                    <stop offset="1" stopColor="#2f9a20" />
                </linearGradient>
                <linearGradient id="bm-grey-a" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0" stopColor="#8a8a8a" />
                    <stop offset="1" stopColor="#6b6b6b" />
                </linearGradient>
                <linearGradient id="bm-grey-b" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0" stopColor="#6f6f6f" />
                    <stop offset="1" stopColor="#4f4f4f" />
                </linearGradient>
            </defs>

            <path d="M32 1.5 58.5 16.75v30.5L32 62.5 5.5 47.25v-30.5z" fill="#242429" />
            <path d="M32 32 32 6 55 19.5z" fill="url(#bm-grey-a)" />
            <path d="M32 32 55 19.5 55 44.5z" fill="url(#bm-green)" />
            <path d="M32 32 55 44.5 32 58z" fill="url(#bm-grey-b)" />
            <path d="M32 32 32 58 9 44.5z" fill="url(#bm-grey-a)" />
            <path d="M32 32 9 44.5 9 19.5z" fill="url(#bm-grey-b)" />
            <path d="M32 32 9 19.5 32 6z" fill="url(#bm-grey-a)" />
            <path d="M32 24.5 38.5 28.25v7.5L32 39.5 25.5 35.75v-7.5z" fill="#0c0607" />
        </svg>
    )
}
