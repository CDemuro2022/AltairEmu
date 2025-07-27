# AltairEmu

A fully-functional, cycle-accurate* emulation of the Altair 8800 and its front panel. This emulator currently provides no peripherals whatsoever, you get the Altair front panel and 64K of empty RAM. However, it is possible peripherals like serial boards and cassette interfaces will be added in future updates. All states are emulated, though certain operations are not performed during the correct state or through accurate means, either due to simplicity or documentation ambiguity. All timing is accurate, and every instruction is accurate per machine-cycle.

## Operation

Just like a real Altair, when the machine starts, it is in an undefined state. This can be fixed by hitting the RESET switch. But still, you just have an empty 64K of RAM with no program. Toggle in a program using the switches on the front. [Kill the Bit](https://altairclone.com/downloads/killbits.pdf) is a great first test!!

## Limitations

No peripherals are provided. Only the CPU, 64K of RAM, and the Altair front panel are emulated.

The Interrupt Acknowledge light has been omitted, as there is no emulated hardware capable of generating interrupts.

The Protect/Unprotect switch and light have been omitted, as this was rarely utilized on a real Altair and is poorly documented.

The Aux switches have been omitted, as there is no emulated hardware to connect them to.

## Known Issues

The CPU currently does not pass all tests of the Intel 8080 Exerciser (8080EXM.COM), with the ALUOP nn, ALUOP r, and DAA,CMA,STC,CMC test groups all still giving errors. However, these errors appear to be minor and should not affect most software that can be run on the front panel. All other CPU tests available pass.

The bus pull-ups for the front panel and the buffers are not fully emulated, however, this should not affect software functionality. At worst, data bus lights may show an improper value during write operations and status other lights may have improver states in certain edge cases.

## Requirements

- .NET 8.0
- Patience

## Demo Video

https://github.com/user-attachments/assets/412beda6-401d-4210-bfa8-03beaba70871

