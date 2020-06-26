# Redpwn-SMRT-Solver-CS
My first ever writeup so if its bad, my bad lol.

So when I approached this problem, first thing I did was pop it into IDA, as one does,\
and after navigating to the main function, we are treated with a lovely suprise:\
![Ida1](/img/ida1.gif)\
\
A 40K+ line assembly chunk in main. This ofc means any decompiler is going to fail,\
and looking closer at the function we notice all the jumps are `jbe` and `jnb`, meaning all the compareops are relative.\
![Ida2](/img/ida2.png)
\
Further inspecting the operators of the jumps shows that each operator is some offset of the input string,\
and each of the jumps are to a `nop` and ultimately, the incorrect puts.\

![Ida3](/img/ida3.png)

![Ida4](/img/ida4.png)
\
We also can inspect a couple more important aspects of the logic\

![Ida5](/img/ida5.png)
\
This shows that the input length must be somewhere from 0x49 to 0x100 characters long.\
\
We also notice that the newline in the input buffer is replaced with null.\
\
Some very brief analysis of the variables IDA picked up on shows that only the first 0x48 characters are even referenced,\
so we know for a fact that the input flag is exactly 0x49 in length.\
![Ida6](/img/ida6.png)
