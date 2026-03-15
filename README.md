# StacksCoolSlower
## Overview
StacksCoolSlower is a Vintage Story mod that makes stacked items cool down slower than individual items. This creates a more realistic simulation where larger stacks of items retain heat longer, making the game's temperature mechanics more intuitive.

## How It Works
The mod uses Harmony to patch the temperature calculation methods in Vintage Story's CollectibleObject class. Specifically:
* It patches the GetTemperature method that handles item cooling
* For stacked items, the cooling rate is divided by the stack size
* This means a stack of 10 items will cool 10 times slower than a single item

The core functionality is in the ModifyCooling method which adjusts the cooling rate based on stack size:

```C#
public static double ModifyCooling(float number, ItemStack itemstack, object closure = null)
{
    if(closure != null)
    {
        var field = closure.GetType().GetField("itemstack");
        ItemStack itemStack = (ItemStack)field.GetValue(closure);

        if(itemStack != null && itemStack.StackSize > 1)
        {
            number /= itemStack.StackSize;
        }
    }
    else
    {
        if (itemstack != null && itemstack.StackSize > 1)
        {
            number /= itemstack.StackSize;
        }
    }

    return number;
}
```
## Technical Implementation
The mod:
1. Uses Harmony to transpile two different versions of the GetTemperature method.
2. Injects calls to the ModifyCooling method at specific points in the code
3. Handles both direct calls and closure-based calls to ensure all cooling paths are covered
4. Carefully identifies injection points by analyzing the IL code pattern
## Installation
1. Download the latest release
2. Place the .zip file in your Vintage Story Mods folder
3. Launch the game and enable the mod
## Requirements
* Vintage Story 1.22 or higher
* Harmony library (included with the game)
## Compatibility
This mod should be compatible with most other Vintage Story mods as it only affects the temperature calculation for items.

License
MIT

