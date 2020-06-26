# Redpwn-SMRT-Solver-CS
My first ever writeup so if its bad, my bad lol.

So when I approached this problem, first thing I did was pop it into IDA, as one does,\
and after navigating to the main function, we are treated with a lovely suprise:\
### A 40K+ line assembly chunk in main.
![Ida1](/img/ida1.gif)\
\
This of course means any decompiler is going to fail,\
and looking closer at the function we notice all the jumps are `jbe` and `jnb`, meaning all the compareops are relative.\
![Ida2](/img/ida2.png)\
\
Further inspecting the operators of the jumps shows that each operator is some offset of the input string,\
and each of the jumps are to a `nop` and ultimately, the incorrect puts.

![Ida3](/img/ida3.png)\

![Ida4](/img/ida4.png)\
\
We also can inspect a couple more important aspects of the logic

![Ida5](/img/ida5.png)\
\
This shows that the input length must be somewhere from 0x49 to 0x100 characters long.\
\
We also notice that the newline in the input buffer is replaced with null.\
\
Some very brief analysis of the variables IDA picked up on shows that only the first 0x48 characters are even referenced,\
so we know for a fact that the input flag is exactly 0x49 in length.\
![Ida6](/img/ida6.png)\
\
Further down, when inspecting the win condition, we recognize that all characters will meet c `isalpha` specifications.
![Ida7](/img/ida7.png)\
\
This introduces a bug in the algorithm, unfortunately, but we will address that later.
We now have enough information to intelligently bruteforce the solution.

# Algorithm
This section will basically just be the thought process I took in writing the code\
\
\
First, I cleaned up the assembly to normalize it a bit for parsing\
\
Next, I wrote an `ASMLine` super class for the assembly instructions, and implemented a sectional dictionary. This allowed jumps to take a o(1) jump to the next instruction without requiring a tree for recursion. I also included a virtual stack, two registers, and a callstack\
\
Each `ASMLine` also acts as a dually linked list for special traversal occurances where the callstack isnt necessary\
\
With this setup the goal is simple:

* For each block:
  * Populate the registers required
  * Simulate the compare operator and jump
  * When a jump to a death occurs, back up a bit and try to figure out why we died.
  * Attempt to remedy the problem by adjusting the input string and restarting the asm walk.
  * When we reach the win section, we have the flag!

Note a few special cases and rules:
  * The flag format is flag{xxxxxx} meaning we can anchor those characters in the start
  * Lets use a fixed seed to make sure we can reproduce bugs consistently
  * We also *cannot* allow the anchored characters to be altered to meet conditions because this will alter the win condition, thus creating an infinite loop.
  * We have to use *lower case only* to meet the flag condition. Uppercase results in many, many win conditions.

# GG
After we setup these rules, we essentially just run and wait. It only takes ~10s to get the flag on normal runs.\
From a data science standpoint, im happy with that. I mean, bruteforcing until the end of time versus 10s lol\
Anyways, nice challenge. I really enjoyed this one.
