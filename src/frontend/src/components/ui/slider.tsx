import * as React from "react"
import { cn } from "@/lib/utils"

const Slider = React.forwardRef<
  React.ElementRef<"input">,
  React.ComponentPropsWithoutRef<"input">
>(({ className, ...props }, ref) => (
  <input
    type="range"
    ref={ref}
    className={cn(
      "relative flex w-full touch-none select-none items-center h-2 rounded-full bg-navy-800",
      "accent-gold-500",
      className
    )}
    {...props}
  />
))
Slider.displayName = "Slider"

export { Slider }
